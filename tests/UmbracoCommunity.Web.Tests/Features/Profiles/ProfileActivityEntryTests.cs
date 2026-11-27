using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Mvp.Models;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>
/// Activity added by hand, end to end: added and edited through the member API into the real store (on the profile at
/// once), removed or restored through the activity entry service as a moderator would, and read back through the profile
/// provider and activity service as the profile page does.
/// </summary>
public class ProfileActivityEntryTests : IDisposable
{
    private const string Handle = "octocat";
    private const string Link = "https://example.dev/my-post";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly MemberProfileDataProvider _provider;
    private readonly Mock<IProfileSyncClient> _syncClient = new();
    private readonly ProfileActivityEntryService _activityEntryService;
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly InMemoryMediaFileSystem _media = new();
    private readonly ProfileActivityImageStore _images;

    public ProfileActivityEntryTests()
    {
        _images = new ProfileActivityImageStore(_media.FileSystem, NullLogger<ProfileActivityImageStore>.Instance);
        _store = new MemberProfileStore(_database);
        _provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>(), Mock.Of<Umbraco.Cms.Core.Media.IImageUrlGenerator>()),
            TestImageProxy.Create());
        _activityEntryService = new ProfileActivityEntryService(
            new MemberActivityEntryStore(_database),
            _syncClient.Object,
            _images,
            Caches(),
            NullLogger<ProfileActivityEntryService>.Instance);

        using var context = _database.CreateDbContext();
        context.Database.Migrate();
    }

    public void Dispose() => _database.Dispose();

    private static AppCaches Caches() => new(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));

    private async Task CreateProfileAsync()
    {
        await _store.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
        await _store.CompleteOnboardingAsync(_memberKey);
    }

    private ProfileActivityEntryApiController CreateController(bool inEarlyAccess = true)
    {
        var member = new MemberIdentityUser { Key = _memberKey, UserName = Handle };
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        memberManager
            .Setup(m => m.GetRolesAsync(member))
            .ReturnsAsync(new List<string> { inEarlyAccess ? ProfileAccessService.EarlyAccessGroupName : "Community Members" });

        var access = new ProfileAccessService(memberManager.Object, _provider, NullLogger<ProfileAccessService>.Instance);
        return new ProfileActivityEntryApiController(
            memberManager.Object, _store, access, _activityEntryService, _images, ProfileActivityLinkLookupTests.Create(_database, _media));
    }

    private ProfileActivityService CreateActivityService() =>
        new(
            _syncClient.Object,
            _activityEntryService,
            new ProfileMatchStore(_database),
            Mock.Of<IMvpDataService>(m => m.GetAll() == Array.Empty<MvpYear>()),
            Caches(),
            Mock.Of<IOptionsMonitor<ProfileActivityOptions>>(o => o.CurrentValue == new ProfileActivityOptions()),
            NullLogger<ProfileActivityService>.Instance);

    private async Task<CommunityProfile> ProfileAsync()
    {
        var profile = await _provider.GetProfileAsync(Handle, includeHidden: true);
        return await CreateActivityService().EnrichAsync(profile!);
    }

    private static AddActivityEntryRequest Entry(string url = Link, string kind = "blog") =>
        new(kind, "My post", url, new DateOnly(2026, 10, 1), "About a thing.");

    /// <summary>The entry as the account page posts it, with an optional picture.</summary>
    private static AddActivityEntryForm Form(AddActivityEntryRequest request, IFormFile? image = null) =>
        new()
        {
            Kind = request.Kind,
            Title = request.Title,
            Url = request.Url,
            Date = request.Date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Description = request.Description,
            Image = image,
        };

    private static MemberActivityEntryResponse Added(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MemberActivityEntryResponse>().Subject;

    private static string? Error(IActionResult result) =>
        ((ObjectResult)result).Value!.GetType().GetProperty("error")!.GetValue(((ObjectResult)result).Value) as string;

    private async Task<IReadOnlyList<MemberActivityEntryResponse>> MineAsync(ProfileActivityEntryApiController controller) =>
        (await controller.Get()).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IReadOnlyList<MemberActivityEntryResponse>>().Subject;

    /// <summary>An entry added while entries were still checked first, and never decided: stored as waiting.</summary>
    private async Task<int> AddLegacyWaitingEntryAsync(string url = Link)
    {
        var profile = await _store.GetByMemberKeyAsync(_memberKey);
        await using var context = _database.CreateDbContext();
        var entity = new MemberActivityEntryEntity
        {
            MemberProfileId = profile!.Id,
            Kind = "blog",
            Title = "Old post",
            Url = url,
            DateUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            State = ActivityEntryState.Pending,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
        };
        context.MemberActivityEntries.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    private static UpdateActivityEntryForm EditForm(AddActivityEntryRequest request, bool removeImage = false) =>
        new()
        {
            Kind = request.Kind,
            Title = request.Title,
            Url = request.Url,
            Date = request.Date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Description = request.Description,
            RemoveImage = removeImage,
        };

    // ---- rules ----

    [Fact]
    public void A_complete_entry_is_normalised()
    {
        var request = new AddActivityEntryRequest(" BLOG ", "  My post ", " https://example.dev/a ", new DateOnly(2026, 10, 1), "  ");

        ProfileActivityEntry.TryNormalize(request, new DateOnly(2026, 11, 9), out var normalized, out _).Should().BeTrue();

        normalized.Should().Be(new AddActivityEntryRequest("blog", "My post", "https://example.dev/a", new DateOnly(2026, 10, 1), null));
    }

    [Theory]
    [InlineData("poem", "T", "https://example.dev/a", "2026-10-01")]
    [InlineData("blog", " ", "https://example.dev/a", "2026-10-01")]
    [InlineData("blog", "T", "http://example.dev/a", "2026-10-01")]
    [InlineData("blog", "T", "https://user:pw@example.dev/a", "2026-10-01")]
    [InlineData("blog", "T", "javascript:alert(1)", "2026-10-01")]
    [InlineData("blog", "T", "https://example.dev/a", "2026-11-10")]
    [InlineData("blog", "T", "https://example.dev/a", "1980-01-01")]
    [InlineData("blog", "T", "https://example.dev/a", null)]
    public void Invalid_entries_are_refused_with_a_message(string kind, string title, string url, string? date)
    {
        var request = new AddActivityEntryRequest(kind, title, url, date == null ? null : DateOnly.Parse(date), null);

        ProfileActivityEntry.TryNormalize(request, new DateOnly(2026, 11, 9), out _, out var error).Should().BeFalse();

        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void An_over_long_title_or_description_is_refused()
    {
        var today = new DateOnly(2026, 11, 9);

        ProfileActivityEntry.TryNormalize(Entry() with { Title = new string('a', ProfileActivityEntry.MaxTitleLength + 1) }, today, out _, out _).Should().BeFalse();
        ProfileActivityEntry.TryNormalize(Entry() with { Description = new string('a', ProfileActivityEntry.MaxDescriptionLength + 1) }, today, out _, out _).Should().BeFalse();
    }

    // ---- add ----

    [Fact]
    public async Task An_added_entry_is_on_the_profile_at_once_and_the_platform_is_told()
    {
        await CreateProfileAsync();
        // Loaded once, so a stale cached copy would show if adding didn't clear it.
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();

        var added = Added(await CreateController().Add(Form(Entry())));

        added.State.Should().Be("Verified");
        added.KindLabel.Should().Be("Blog post");
        var post = (await ProfileAsync()).BlogPosts.Should().ContainSingle().Subject;
        post.Title.Should().Be("My post");
        post.Url.Should().Be(Link);
        post.ActivityId.Should().Be(ProfileActivityEntry.ActivityId(added.Id));
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(Handle, It.Is<ActivityEntryRecord>(e => e.Url == Link && e.Id == added.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Members_outside_early_access_cannot_add_edit_or_see_activity()
    {
        await CreateProfileAsync();
        var controller = CreateController(inEarlyAccess: false);

        (await controller.Add(Form(Entry()))).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await controller.Get()).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await controller.Update(1, EditForm(Entry()))).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task A_bad_entry_is_a_400_and_stores_nothing()
    {
        await CreateProfileAsync();
        var controller = CreateController();

        (await controller.Add(Form(Entry("http://example.dev/a")))).Should().BeOfType<BadRequestObjectResult>();

        (await MineAsync(controller)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_link_cannot_be_added_twice_but_there_is_no_cap_on_how_many_entries()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        Added(await controller.Add(Form(Entry())));

        var again = await controller.Add(Form(Entry(Link.ToUpperInvariant().Replace("HTTPS", "https"))));
        again.Should().BeOfType<ConflictObjectResult>();
        Error(again).Should().Be("You've already added that link.");

        for (var i = 1; i <= 25; i++)
        {
            Added(await controller.Add(Form(Entry($"https://example.dev/post-{i}"))));
        }

        (await MineAsync(controller)).Should().HaveCount(26);
    }

    [Fact]
    public async Task A_platform_failure_does_not_undo_an_add()
    {
        _syncClient
            .Setup(c => c.NotifyActivityVerifiedAsync(It.IsAny<string>(), It.IsAny<ActivityEntryRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("simulated"));
        await CreateProfileAsync();

        Added(await CreateController().Add(Form(Entry())));

        (await ProfileAsync()).BlogPosts.Should().ContainSingle();
    }

    // ---- withdraw ----

    [Fact]
    public async Task Another_entry_id_or_an_unknown_one_cannot_be_withdrawn()
    {
        await CreateProfileAsync();

        (await CreateController().Withdraw(9999)).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Withdrawing_an_entry_takes_it_off_the_profile_at_once_and_it_cannot_be_restored()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        (await ProfileAsync()).BlogPosts.Should().ContainSingle();

        (await controller.Withdraw(added.Id)).Should().BeOfType<NoContentResult>();

        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
        (await MineAsync(controller)).Should().BeEmpty();
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(Handle, Link, It.IsAny<CancellationToken>()), Times.Once);
        (await _activityEntryService.VerifyAsync(added.Id, null, "Moderator")).Should().BeNull();
        // A link the member withdrew themselves can be added again.
        Added(await controller.Add(Form(Entry())));
    }

    // ---- where each kind shows ----

    [Fact]
    public async Task Entries_land_in_their_own_section_and_can_be_hidden_like_any_item()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var video = Added(await controller.Add(Form(Entry("https://example.dev/v", "videos"))));
        var package = Added(await controller.Add(Form(Entry("https://example.dev/p", "packages"))));

        var profile = await ProfileAsync();

        profile.Videos.Should().ContainSingle().Which.Url.Should().Be("https://example.dev/v");
        profile.Packages.Should().ContainSingle().Which.Url.Should().Be("https://example.dev/p");
        ProfileDisplay.ActivityItems(profile).Select(i => i.ActivityId)
            .Should().BeEquivalentTo(ProfileActivityEntry.ActivityId(video.Id), ProfileActivityEntry.ActivityId(package.Id));
    }

    [Fact]
    public async Task An_event_appears_under_meet_ups()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Form(Entry("https://example.dev/e", "event"))));

        var meetup = (await ProfileAsync()).Meetups.Should().ContainSingle().Subject;

        meetup.EventName.Should().Be("My post");
        meetup.Url.Should().Be("https://example.dev/e");
        meetup.ActivityId.Should().Be(ProfileActivityEntry.ActivityId(added.Id));
    }

    [Theory]
    [InlineData("talk", "Talk")]
    [InlineData("contribution", "Open-source contribution")]
    [InlineData("podcast", "Podcast or interview")]
    [InlineData("other", "Other")]
    public async Task Other_kinds_appear_under_other_activity_and_can_be_hidden_like_any_item(string kind, string label)
    {
        await CreateProfileAsync();
        Added(await CreateController().Add(Form(Entry("https://example.dev/o", kind))));

        var profile = await ProfileAsync();

        var activity = profile.OtherActivities.Should().ContainSingle().Subject;
        activity.Kind.Should().Be(label);
        ProfileDisplay.ActivityItems(profile).Should().ContainSingle(i => i.Group == PlatformActivityGroups.Other)
            .Which.Source.Should().Be(label);
        ProfileDisplay.Apply(profile with
        {
            DisplayChoices = new ProfileDisplayChoices([PlatformActivityGroups.Other], [], []),
        }).OtherActivities.Should().BeEmpty();
    }

    [Theory]
    [InlineData("blog")]
    [InlineData("videos")]
    [InlineData("packages")]
    [InlineData("event")]
    [InlineData("talk")]
    public async Task The_featured_copy_of_an_entry_matches_the_item_on_the_profile(string kind)
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Form(Entry("https://example.dev/f", kind))));
        var record = (await _activityEntryService.ListForMemberAsync((await _store.GetByMemberKeyAsync(_memberKey))!.Id)).Single();

        ProfileActivityEntry.FeaturedItem(record).Should().Be(
            ProfileDisplay.ActivityItems(await ProfileAsync()).Single(i => i.ActivityId == ProfileActivityEntry.ActivityId(added.Id)));
    }

    [Fact]
    public async Task A_link_the_platform_already_reports_is_not_shown_twice()
    {
        await CreateProfileAsync();
        Added(await CreateController().Add(Form(Entry())));
        _syncClient
            .Setup(c => c.FindProfileAsync(Handle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformProfile("platform-1", []));
        _syncClient
            .Setup(c => c.GetActivitiesAsync("platform-1", PlatformActivityGroups.Blog, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlatformActivity("b1", "x:y", "web", "My post", Link, null, null, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero))]);
        _syncClient
            .Setup(c => c.GetActivitiesAsync("platform-1", It.Is<string>(g => g != PlatformActivityGroups.Blog), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        (await ProfileAsync()).BlogPosts.Should().ContainSingle().Which.ActivityId.Should().Be("b1");
    }

    // ---- moderators remove and restore ----

    [Fact]
    public async Task A_moderator_removes_an_entry_with_a_note_and_it_stops_showing_at_once()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        (await ProfileAsync()).BlogPosts.Should().ContainSingle();

        var removed = await _activityEntryService.RejectAsync(added.Id, "That page isn't yours.", Guid.NewGuid(), "Moderator");

        removed!.State.Should().Be(ActivityEntryState.Rejected);
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(Handle, Link, It.IsAny<CancellationToken>()), Times.Once);
        var mine = (await MineAsync(controller)).Single();
        mine.State.Should().Be("Rejected");
        mine.RejectionReason.Should().Be("That page isn't yours.");
    }

    [Fact]
    public async Task An_entry_cannot_get_the_decision_it_already_has_or_be_decided_once_withdrawn()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        (await _activityEntryService.VerifyAsync(added.Id, null, "Moderator")).Should().BeNull();

        await _activityEntryService.RejectAsync(added.Id, "No.", null, "Moderator");
        (await _activityEntryService.RejectAsync(added.Id, "Again.", null, "Moderator")).Should().BeNull();

        await _activityEntryService.VerifyAsync(added.Id, null, "Moderator");
        (await _activityEntryService.VerifyAsync(added.Id, null, "Moderator")).Should().BeNull();

        await controller.Withdraw(added.Id);
        (await _activityEntryService.VerifyAsync(added.Id, null, "Moderator")).Should().BeNull();
        (await _activityEntryService.RejectAsync(added.Id, "Too late.", null, "Moderator")).Should().BeNull();
    }

    [Fact]
    public async Task A_removed_entry_can_be_restored_and_then_shows_without_its_note()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Form(Entry())));
        await _activityEntryService.RejectAsync(added.Id, "Not sure.", null, "Moderator");

        var restored = await _activityEntryService.VerifyAsync(added.Id, Guid.NewGuid(), "Other moderator");

        restored!.State.Should().Be(ActivityEntryState.Verified);
        restored.RejectionReason.Should().BeNull();
        restored.DecidedByUserName.Should().Be("Other moderator");
        (await ProfileAsync()).BlogPosts.Should().ContainSingle();
        // Once when added, once when restored.
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(Handle, It.IsAny<ActivityEntryRecord>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task A_member_can_withdraw_a_removed_entry_but_cannot_add_its_link_again()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        await _activityEntryService.RejectAsync(added.Id, "Spam.", null, "Moderator");

        var readded = await controller.Add(Form(Entry()));
        readded.Should().BeOfType<ConflictObjectResult>();
        Error(readded).Should().Contain("moderator removed that link");

        (await controller.Withdraw(added.Id)).Should().BeOfType<NoContentResult>();
        (await MineAsync(controller)).Should().BeEmpty();
        // No "removed" call: it had already stopped showing.
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(Handle, Link, It.IsAny<CancellationToken>()), Times.Once);

        (await controller.Add(Form(Entry()))).Should().BeOfType<ConflictObjectResult>();
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
    }

    [Fact]
    public async Task A_pinned_entry_a_moderator_removed_is_no_longer_featured()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Form(Entry())));
        var activityId = ProfileActivityEntry.ActivityId(added.Id);
        var item = ProfileDisplay.ActivityItems(await ProfileAsync()).Single(i => i.ActivityId == activityId);
        await _store.SetDisplayChoicesAsync(_memberKey, new ProfileDisplayChoices([], [], [new ProfilePin(activityId, "Proud", item)]));
        ProfileDisplay.Apply(await ProfileAsync()).FeaturedItems.Should().ContainSingle();

        await _activityEntryService.RejectAsync(added.Id, "Spam.", null, "Moderator");

        ProfileDisplay.Apply(await ProfileAsync()).FeaturedItems.Should().BeEmpty();
    }

    // ---- legacy entries still waiting ----

    [Fact]
    public async Task A_legacy_waiting_entry_stays_off_the_profile_and_is_listed_for_the_member_and_moderators()
    {
        await CreateProfileAsync();
        var id = await AddLegacyWaitingEntryAsync();

        (await MineAsync(CreateController())).Should().ContainSingle().Which.State.Should().Be("Pending");
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
        (await _activityEntryService.ListAsync(ActivityEntryState.Pending, 0, 10)).Items.Should().ContainSingle(e => e.Id == id);

        var shown = await _activityEntryService.VerifyAsync(id, null, "Moderator");

        shown!.State.Should().Be(ActivityEntryState.Verified);
        (await ProfileAsync()).BlogPosts.Should().ContainSingle();
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(Handle, It.Is<ActivityEntryRecord>(e => e.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_legacy_waiting_entry_can_be_removed_withdrawn_or_edited_and_keeps_waiting()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var edited = await AddLegacyWaitingEntryAsync("https://example.dev/a");
        var withdrawn = await AddLegacyWaitingEntryAsync("https://example.dev/b");
        var removed = await AddLegacyWaitingEntryAsync("https://example.dev/c");

        var after = Added(await controller.Update(edited, EditForm(Entry("https://example.dev/a") with { Title = "Better title" })));
        (await controller.Withdraw(withdrawn)).Should().BeOfType<NoContentResult>();
        (await _activityEntryService.RejectAsync(removed, "No.", null, "Moderator")).Should().NotBeNull();

        after.State.Should().Be("Pending");
        after.Title.Should().Be("Better title");
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(It.IsAny<string>(), It.IsAny<ActivityEntryRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- edit ----

    [Fact]
    public async Task Editing_an_entry_keeps_its_id_and_state_updates_the_profile_and_announces_it_again()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        (await ProfileAsync()).BlogPosts.Single().Title.Should().Be("My post");

        var edited = Added(await controller.Update(added.Id, EditForm(Entry() with { Title = "My better post", Description = "Now with words." })));

        edited.Id.Should().Be(added.Id);
        edited.State.Should().Be("Verified");
        edited.Title.Should().Be("My better post");
        var post = (await ProfileAsync()).BlogPosts.Should().ContainSingle().Subject;
        post.Title.Should().Be("My better post");
        post.ActivityId.Should().Be(ProfileActivityEntry.ActivityId(added.Id));
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(Handle, It.Is<ActivityEntryRecord>(e => e.Title == "My better post"), It.IsAny<CancellationToken>()), Times.Once);
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Changing_the_link_removes_the_old_one_and_announces_the_new_one()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));

        Added(await controller.Update(added.Id, EditForm(Entry("https://example.dev/moved"))));

        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(Handle, Link, It.IsAny<CancellationToken>()), Times.Once);
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(Handle, It.Is<ActivityEntryRecord>(e => e.Url == "https://example.dev/moved"), It.IsAny<CancellationToken>()), Times.Once);
        (await ProfileAsync()).BlogPosts.Single().Url.Should().Be("https://example.dev/moved");
    }

    [Fact]
    public async Task Changing_the_kind_moves_the_entry_to_its_new_section()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));

        Added(await controller.Update(added.Id, EditForm(Entry(kind: "videos"))));

        var profile = await ProfileAsync();
        profile.BlogPosts.Should().BeEmpty();
        profile.Videos.Should().ContainSingle().Which.ActivityId.Should().Be(ProfileActivityEntry.ActivityId(added.Id));
    }

    [Fact]
    public async Task An_edit_cannot_take_the_link_of_another_entry_but_may_keep_its_own()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var first = Added(await controller.Add(Form(Entry())));
        var second = Added(await controller.Add(Form(Entry("https://example.dev/second"))));

        var taken = await controller.Update(second.Id, EditForm(Entry(Link)));
        taken.Should().BeOfType<ConflictObjectResult>();
        Error(taken).Should().Be("You've already added that link.");

        Added(await controller.Update(first.Id, EditForm(Entry(Link.Replace("my-post", "MY-POST")))));
        Added(await controller.Update(second.Id, EditForm(Entry("https://example.dev/second"))));
    }

    [Fact]
    public async Task An_edit_cannot_take_a_link_a_moderator_removed()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var spam = Added(await controller.Add(Form(Entry("https://example.dev/spam"))));
        var other = Added(await controller.Add(Form(Entry())));
        await _activityEntryService.RejectAsync(spam.Id, "Spam.", null, "Moderator");
        await controller.Withdraw(spam.Id);

        (await controller.Update(other.Id, EditForm(Entry("https://example.dev/spam")))).Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task An_entry_a_moderator_removed_cannot_be_edited_back_onto_the_profile()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        await _activityEntryService.RejectAsync(added.Id, "Spam.", null, "Moderator");

        var result = await controller.Update(added.Id, EditForm(Entry() with { Title = "Totally different" }));

        result.Should().BeOfType<ConflictObjectResult>();
        Error(result).Should().Contain("A moderator removed this entry");
        var mine = (await MineAsync(controller)).Single();
        mine.State.Should().Be("Rejected");
        mine.Title.Should().Be("My post");
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_withdrawn_or_other_members_entries_cannot_be_edited()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        await controller.Withdraw(added.Id);

        (await controller.Update(added.Id, EditForm(Entry()))).Should().BeOfType<NotFoundObjectResult>();
        (await controller.Update(9999, EditForm(Entry()))).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task An_edit_with_bad_values_is_a_400_and_changes_nothing()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));

        (await controller.Update(added.Id, EditForm(Entry("http://example.dev/a")))).Should().BeOfType<BadRequestObjectResult>();
        (await controller.Update(added.Id, EditForm(Entry() with { Date = new DateOnly(2999, 1, 1) }))).Should().BeOfType<BadRequestObjectResult>();

        (await MineAsync(controller)).Single().Url.Should().Be(Link);
    }

    [Fact]
    public async Task Editing_a_pinned_entry_refreshes_the_pins_stored_copy()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Entry())));
        var activityId = ProfileActivityEntry.ActivityId(added.Id);
        var item = ProfileDisplay.ActivityItems(await ProfileAsync()).Single(i => i.ActivityId == activityId);
        await _store.SetDisplayChoicesAsync(_memberKey, new ProfileDisplayChoices([], [], [new ProfilePin(activityId, "Proud", item)]));

        Added(await controller.Update(added.Id, EditForm(Entry("https://example.dev/talk", "talk") with { Title = "Renamed" })));

        var pin = (await _provider.GetProfileAsync(Handle, includeHidden: true))!.DisplayChoices.Pins.Single();
        pin.ActivityId.Should().Be(activityId);
        pin.Reason.Should().Be("Proud");
        pin.Snapshot!.Title.Should().Be("Renamed");
        pin.Snapshot.Url.Should().Be("https://example.dev/talk");
        pin.Snapshot.Group.Should().Be(PlatformActivityGroups.Other);
        pin.Snapshot.Source.Should().Be("Talk");
        ProfileDisplay.Apply(await ProfileAsync()).FeaturedItems.Single().Title.Should().Be("Renamed");
    }
}

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
/// Activity added by hand, end to end: submitted through the member API into the real store, checked through the
/// activity entry service as an admin would, and read back through the profile provider and activity service as the profile
/// page does.
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

    public ProfileActivityEntryTests()
    {
        _store = new MemberProfileStore(_database);
        _provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>()),
            TestImageProxy.Create());
        _activityEntryService = new ProfileActivityEntryService(
            new MemberActivityEntryStore(_database),
            _syncClient.Object,
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
        return new ProfileActivityEntryApiController(memberManager.Object, _store, access, _activityEntryService);
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

    private static MemberActivityEntryResponse Added(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MemberActivityEntryResponse>().Subject;

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

    // ---- submit ----

    [Fact]
    public async Task A_submitted_entry_waits_and_is_not_on_the_profile()
    {
        await CreateProfileAsync();

        var added = Added(await CreateController().Add(Entry()));

        added.State.Should().Be("Pending");
        added.KindLabel.Should().Be("Blog post");
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
    }

    [Fact]
    public async Task Members_outside_early_access_cannot_add_or_see_activity()
    {
        await CreateProfileAsync();
        var controller = CreateController(inEarlyAccess: false);

        (await controller.Add(Entry())).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await controller.Get()).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task A_bad_entry_is_a_400_and_stores_nothing()
    {
        await CreateProfileAsync();
        var controller = CreateController();

        (await controller.Add(Entry("http://example.dev/a"))).Should().BeOfType<BadRequestObjectResult>();

        (await controller.Get()).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<MemberActivityEntryResponse>>().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_link_cannot_be_added_twice_and_too_many_waiting_entries_are_refused()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        Added(await controller.Add(Entry()));

        (await controller.Add(Entry(Link.ToUpperInvariant().Replace("HTTPS", "https")))).Should().BeOfType<ConflictObjectResult>();

        for (var i = 1; i < ProfileActivityEntry.MaxPendingEntries; i++)
        {
            Added(await controller.Add(Entry($"https://example.dev/post-{i}")));
        }

        (await controller.Add(Entry("https://example.dev/one-more"))).Should().BeOfType<ConflictObjectResult>();
    }

    // ---- withdraw ----

    [Fact]
    public async Task A_waiting_entry_can_be_withdrawn_and_then_cannot_be_verified()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Entry()));

        (await controller.Withdraw(added.Id)).Should().BeOfType<NoContentResult>();

        (await controller.Get()).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<MemberActivityEntryResponse>>().Which.Should().BeEmpty();
        (await _activityEntryService.VerifyAsync(added.Id, null, "Admin")).Should().BeNull();
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Another_entry_id_or_an_unknown_one_cannot_be_withdrawn()
    {
        await CreateProfileAsync();

        (await CreateController().Withdraw(9999)).Should().BeOfType<NotFoundObjectResult>();
    }

    // ---- verify ----

    [Fact]
    public async Task A_verified_entry_appears_in_its_section_and_the_platform_is_told()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Entry()));

        var verified = await _activityEntryService.VerifyAsync(added.Id, Guid.NewGuid(), "Admin");

        verified!.State.Should().Be(ActivityEntryState.Verified);
        verified.DecidedByUserName.Should().Be("Admin");
        var post = (await ProfileAsync()).BlogPosts.Should().ContainSingle().Subject;
        post.Title.Should().Be("My post");
        post.Url.Should().Be(Link);
        post.ActivityId.Should().Be(ProfileActivityEntry.ActivityId(added.Id));
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(Handle, It.Is<ActivityEntryRecord>(e => e.Url == Link), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verified_entries_land_in_their_own_section_and_can_be_hidden_like_any_item()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var video = Added(await controller.Add(Entry("https://example.dev/v", "videos")));
        var package = Added(await controller.Add(Entry("https://example.dev/p", "packages")));
        await _activityEntryService.VerifyAsync(video.Id, null, "Admin");
        await _activityEntryService.VerifyAsync(package.Id, null, "Admin");

        var profile = await ProfileAsync();

        profile.Videos.Should().ContainSingle().Which.Url.Should().Be("https://example.dev/v");
        profile.Packages.Should().ContainSingle().Which.Url.Should().Be("https://example.dev/p");
        ProfileDisplay.ActivityItems(profile).Select(i => i.ActivityId)
            .Should().BeEquivalentTo(ProfileActivityEntry.ActivityId(video.Id), ProfileActivityEntry.ActivityId(package.Id));
    }

    [Fact]
    public async Task A_verified_event_appears_under_meet_ups()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Entry("https://example.dev/e", "event")));
        await _activityEntryService.VerifyAsync(added.Id, null, "Admin");

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
        var added = Added(await CreateController().Add(Entry("https://example.dev/o", kind)));
        await _activityEntryService.VerifyAsync(added.Id, null, "Admin");

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

    [Fact]
    public async Task A_platform_failure_does_not_undo_a_verification()
    {
        _syncClient
            .Setup(c => c.NotifyActivityVerifiedAsync(It.IsAny<string>(), It.IsAny<ActivityEntryRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("simulated"));
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Entry()));

        (await _activityEntryService.VerifyAsync(added.Id, null, "Admin")).Should().NotBeNull();

        (await ProfileAsync()).BlogPosts.Should().ContainSingle();
    }

    [Fact]
    public async Task A_link_the_platform_already_reports_is_not_shown_twice()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Entry()));
        await _activityEntryService.VerifyAsync(added.Id, null, "Admin");
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

    [Fact]
    public async Task Withdrawing_a_verified_entry_takes_it_off_the_profile_at_once()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Entry()));
        await _activityEntryService.VerifyAsync(added.Id, null, "Admin");
        (await ProfileAsync()).BlogPosts.Should().ContainSingle();

        (await controller.Withdraw(added.Id)).Should().BeOfType<NoContentResult>();

        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(Handle, Link, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- reject ----

    [Fact]
    public async Task A_rejected_entry_keeps_its_reason_for_the_member_and_never_shows()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Entry()));

        var rejected = await _activityEntryService.RejectAsync(added.Id, "That page isn't yours.", Guid.NewGuid(), "Admin");

        rejected!.State.Should().Be(ActivityEntryState.Rejected);
        var mine = (await controller.Get()).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<MemberActivityEntryResponse>>().Subject.Single();
        mine.State.Should().Be("Rejected");
        mine.RejectionReason.Should().Be("That page isn't yours.");
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(It.IsAny<string>(), It.IsAny<ActivityEntryRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_entry_cannot_get_the_decision_it_already_has_or_be_decided_once_withdrawn()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Entry()));
        await _activityEntryService.RejectAsync(added.Id, "No.", null, "Admin");
        (await _activityEntryService.RejectAsync(added.Id, "Again.", null, "Admin")).Should().BeNull();

        await _activityEntryService.VerifyAsync(added.Id, null, "Admin");
        (await _activityEntryService.VerifyAsync(added.Id, null, "Admin")).Should().BeNull();

        await controller.Withdraw(added.Id);
        (await _activityEntryService.VerifyAsync(added.Id, null, "Admin")).Should().BeNull();
        (await _activityEntryService.RejectAsync(added.Id, "Too late.", null, "Admin")).Should().BeNull();
    }

    [Fact]
    public async Task A_verified_entry_can_be_rejected_after_all_and_stops_showing_at_once()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Entry()));
        await _activityEntryService.VerifyAsync(added.Id, null, "Admin");
        (await ProfileAsync()).BlogPosts.Should().ContainSingle();

        var rejected = await _activityEntryService.RejectAsync(added.Id, "On second thought, no.", null, "Other admin");

        rejected!.State.Should().Be(ActivityEntryState.Rejected);
        (await ProfileAsync()).BlogPosts.Should().BeEmpty();
        _syncClient.Verify(c => c.NotifyActivityRemovedAsync(Handle, Link, It.IsAny<CancellationToken>()), Times.Once);
        var mine = (await controller.Get()).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<MemberActivityEntryResponse>>().Subject.Single();
        mine.State.Should().Be("Rejected");
        mine.RejectionReason.Should().Be("On second thought, no.");
    }

    [Fact]
    public async Task A_rejected_entry_can_be_verified_after_all_and_then_shows_without_its_reason()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Entry()));
        await _activityEntryService.RejectAsync(added.Id, "Not sure.", null, "Admin");

        var verified = await _activityEntryService.VerifyAsync(added.Id, Guid.NewGuid(), "Other admin");

        verified!.State.Should().Be(ActivityEntryState.Verified);
        verified.RejectionReason.Should().BeNull();
        verified.DecidedByUserName.Should().Be("Other admin");
        (await ProfileAsync()).BlogPosts.Should().ContainSingle();
        _syncClient.Verify(c => c.NotifyActivityVerifiedAsync(Handle, It.IsAny<ActivityEntryRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

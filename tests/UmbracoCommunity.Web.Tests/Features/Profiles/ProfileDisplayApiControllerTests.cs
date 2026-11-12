using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
/// Runs the display choices end to end: saved through the API into the real store, then read back
/// through the profile provider and applied to the member's activity as the profile page does.
/// </summary>
public class ProfileDisplayApiControllerTests : IDisposable
{
    private const string Handle = "octocat";

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly MemberProfileDataProvider _provider;
    private readonly Guid _memberKey = Guid.NewGuid();

    // The member's latest activity as the external content platform returns it, per section.
    private readonly Dictionary<string, IReadOnlyList<PlatformActivity>> _activity = new()
    {
        [PlatformActivityGroups.Blog] = [At("b1", "First post"), At("b2", "Second post")],
        [PlatformActivityGroups.Videos] = [At("v1", "A video")],
        [PlatformActivityGroups.Packages] = [At("p1", "A package")],
    };

    public ProfileDisplayApiControllerTests()
    {
        _store = new MemberProfileStore(_database);
        _provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>()),
            TestImageProxy.Create());
    }

    public void Dispose() => _database.Dispose();

    private async Task CreateProfileAsync(bool completeOnboarding = true)
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        await _store.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
        if (completeOnboarding)
        {
            await _store.CompleteOnboardingAsync(_memberKey);
        }
    }

    private ProfileDisplayApiController CreateController(bool signedIn = true, bool inEarlyAccess = true)
    {
        var member = signedIn ? new MemberIdentityUser { Key = _memberKey, UserName = Handle } : null;
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(signedIn);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        if (member != null)
        {
            memberManager
                .Setup(m => m.GetRolesAsync(member))
                .ReturnsAsync(new List<string> { inEarlyAccess ? ProfileAccessService.EarlyAccessGroupName : "Community Members" });
        }

        var accessService = new ProfileAccessService(memberManager.Object, _provider, NullLogger<ProfileAccessService>.Instance);
        return new ProfileDisplayApiController(memberManager.Object, _store, accessService, _provider, CreateActivityService());
    }

    private static PlatformActivity At(string id, string title) => new(id, "x:y", "web", title, $"https://example.dev/{id}", null, null, Now);

    private ProfileActivityService CreateActivityService()
    {
        IReadOnlyList<PlatformActivity> Items(string group) => _activity.GetValueOrDefault(group) ?? [];

        var syncClient = new Mock<IProfileSyncClient>();
        syncClient
            .Setup(c => c.FindProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformProfile("platform-1", []));
        syncClient
            .Setup(c => c.GetActivitiesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string group, int _, CancellationToken _) => Items(group));

        return new ProfileActivityService(
            syncClient.Object,
            new ProfileActivityEntryService(
                new MemberActivityEntryStore(_database),
                syncClient.Object,
                new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
                NullLogger<ProfileActivityEntryService>.Instance),
            new ProfileMatchStore(_database),
            Mock.Of<IMvpDataService>(m => m.GetAll() == Array.Empty<MvpYear>()),
            new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            Mock.Of<IOptionsMonitor<ProfileActivityOptions>>(o => o.CurrentValue == new ProfileActivityOptions()),
            NullLogger<ProfileActivityService>.Instance);
    }

    /// <summary>The member's profile as a visitor sees it once the choices are applied.</summary>
    private async Task<CommunityProfile> PublicProfileAsync()
    {
        var profile = await _provider.GetProfileAsync(Handle, includeHidden: true);
        return ProfileDisplay.Apply(await CreateActivityService().EnrichAsync(profile!));
    }

    private static UpdateProfileDisplayRequest Choices(
        IReadOnlyList<string>? hiddenSections = null,
        IReadOnlyList<string>? hiddenItemIds = null,
        IReadOnlyList<UpdateProfilePinRequest>? pins = null) =>
        new(hiddenSections ?? [], hiddenItemIds ?? [], pins ?? []);

    private static ProfileDisplayChoicesResponse Saved(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ProfileDisplayChoicesResponse>().Subject;

    [Fact]
    public async Task A_new_profile_shows_everything()
    {
        await CreateProfileAsync();

        var profile = await PublicProfileAsync();

        profile.BlogPosts.Should().HaveCount(2);
        profile.Videos.Should().HaveCount(1);
        profile.Packages.Should().HaveCount(1);
        profile.FeaturedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task A_hidden_item_is_left_out_of_the_profile()
    {
        await CreateProfileAsync();

        var saved = Saved(await CreateController().Update(Choices(hiddenItemIds: ["b1"]), default));

        saved.HiddenItemIds.Should().Equal("b1");
        (await PublicProfileAsync()).BlogPosts.Select(p => p.Title).Should().Equal("Second post");
    }

    [Fact]
    public async Task A_hidden_item_shows_again_once_it_is_no_longer_hidden()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        await controller.Update(Choices(hiddenItemIds: ["b1", "v1"]), default);

        var saved = Saved(await controller.Update(Choices(hiddenItemIds: ["v1"]), default));

        saved.HiddenItemIds.Should().Equal("v1");
        var profile = await PublicProfileAsync();
        profile.BlogPosts.Select(p => p.Title).Should().Equal("First post", "Second post");
        profile.Videos.Should().BeEmpty();
    }

    [Fact]
    public async Task A_switched_off_section_is_left_out_and_shows_again_when_switched_on()
    {
        await CreateProfileAsync();
        var controller = CreateController();

        await controller.Update(Choices(hiddenSections: [PlatformActivityGroups.Blog]), default);
        (await PublicProfileAsync()).BlogPosts.Should().BeEmpty();

        await controller.Update(Choices(), default);
        (await PublicProfileAsync()).BlogPosts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Pinned_items_are_featured_with_their_reason()
    {
        await CreateProfileAsync();

        var saved = Saved(await CreateController().Update(
            Choices(pins: [new UpdateProfilePinRequest("v1", "My first talk recording"), new UpdateProfilePinRequest("b2", null)]),
            default));

        saved.Pins.Should().Equal(new ProfilePinResponse("v1", "My first talk recording", "A video"), new ProfilePinResponse("b2", null, "Second post"));
        var featured = (await PublicProfileAsync()).FeaturedItems;
        featured.Select(i => (i.ActivityId, i.Title, i.Reason)).Should().Equal(
            ("v1", "A video", "My first talk recording"),
            ("b2", "Second post", null));
    }

    [Fact]
    public async Task Pinned_items_can_be_reordered()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        await controller.Update(Choices(pins:
        [
            new UpdateProfilePinRequest("b1", "One"),
            new UpdateProfilePinRequest("v1", "Two"),
            new UpdateProfilePinRequest("p1", "Three"),
        ]), default);

        var saved = Saved(await controller.Update(Choices(pins:
        [
            new UpdateProfilePinRequest("p1", "Three"),
            new UpdateProfilePinRequest("b1", "One"),
            new UpdateProfilePinRequest("v1", "Two"),
        ]), default));

        saved.Pins.Select(p => p.Id).Should().Equal("p1", "b1", "v1");
        (await PublicProfileAsync()).FeaturedItems.Select(i => i.ActivityId).Should().Equal("p1", "b1", "v1");
    }

    [Fact]
    public async Task An_item_is_unpinned_by_leaving_it_out()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        await controller.Update(Choices(pins: [new UpdateProfilePinRequest("b1", null), new UpdateProfilePinRequest("v1", null)]), default);

        await controller.Update(Choices(pins: [new UpdateProfilePinRequest("v1", null)]), default);

        (await PublicProfileAsync()).FeaturedItems.Select(i => i.ActivityId).Should().Equal("v1");
    }

    [Fact]
    public async Task A_copy_of_a_pinned_item_is_kept_with_its_pin()
    {
        await CreateProfileAsync();

        await CreateController().Update(Choices(pins: [new UpdateProfilePinRequest("b2", "Proud")]), default);

        var pin = (await _store.GetByMemberKeyAsync(_memberKey))!.PinnedItems.Should().ContainSingle().Subject;
        pin.Should().BeEquivalentTo(new
        {
            ActivityId = "b2",
            Reason = "Proud",
            Group = PlatformActivityGroups.Blog,
            Title = "Second post",
            Url = "https://example.dev/b2",
            DateUtc = Now.UtcDateTime,
            Source = "web",
        });
    }

    [Fact]
    public async Task A_pinned_item_stays_featured_once_it_is_no_longer_in_the_latest_activity()
    {
        await CreateProfileAsync();
        await CreateController().Update(Choices(pins: [new UpdateProfilePinRequest("b1", "Proud"), new UpdateProfilePinRequest("v1", null)]), default);

        _activity[PlatformActivityGroups.Blog] = [At("b3", "Newer post"), At("b2", "Second post")];

        var profile = await PublicProfileAsync();
        profile.BlogPosts.Select(p => p.Title).Should().Equal("Newer post", "Second post");
        profile.FeaturedItems.Select(i => (i.ActivityId, i.Title, i.Url, i.Reason)).Should().Equal(
            ("b1", "First post", "https://example.dev/b1", "Proud"),
            ("v1", "A video", "https://example.dev/v1", null));
    }

    [Fact]
    public async Task A_pinned_item_no_longer_in_the_latest_activity_keeps_its_copy_when_the_choices_are_saved_again()
    {
        await CreateProfileAsync();
        await CreateController().Update(Choices(pins: [new UpdateProfilePinRequest("b1", null), new UpdateProfilePinRequest("v1", null)]), default);
        _activity[PlatformActivityGroups.Blog] = [At("b2", "Second post")];

        var saved = Saved(await CreateController().Update(
            Choices(pins: [new UpdateProfilePinRequest("v1", null), new UpdateProfilePinRequest("b1", "Still proud")]),
            default));

        saved.Pins.Should().Equal(new ProfilePinResponse("v1", null, "A video"), new ProfilePinResponse("b1", "Still proud", "First post"));
        (await PublicProfileAsync()).FeaturedItems.Select(i => (i.Title, i.Reason)).Should().Equal(
            ("A video", null),
            ("First post", "Still proud"));
    }

    [Fact]
    public async Task The_latest_activity_is_preferred_over_the_copy_kept_with_the_pin()
    {
        await CreateProfileAsync();
        await CreateController().Update(Choices(pins: [new UpdateProfilePinRequest("b1", null)]), default);

        _activity[PlatformActivityGroups.Blog] = [At("b1", "First post, renamed")];

        (await PublicProfileAsync()).FeaturedItems.Should().ContainSingle().Which.Title.Should().Be("First post, renamed");
    }

    [Fact]
    public async Task Pinning_an_item_that_is_not_in_the_activity_is_rejected()
    {
        await CreateProfileAsync();

        var result = await CreateController().Update(Choices(pins: [new UpdateProfilePinRequest("unknown", null)]), default);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _store.GetByMemberKeyAsync(_memberKey))!.PinnedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Reading_includes_pinned_items_that_are_no_longer_in_the_latest_activity()
    {
        await CreateProfileAsync();
        await CreateController().Update(Choices(pins: [new UpdateProfilePinRequest("b1", "Proud")]), default);
        _activity[PlatformActivityGroups.Blog] = [At("b2", "Second post")];

        var response = (await CreateController().Get(default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ProfileDisplayResponse>().Subject;

        response.Sections.Single(s => s.Key == PlatformActivityGroups.Blog).Items.Select(i => i.Id).Should().Equal("b2");
        response.Choices.Pins.Should().Equal(new ProfilePinResponse("b1", "Proud", "First post"));
    }

    [Fact]
    public async Task A_fourth_pin_is_rejected_and_nothing_is_saved()
    {
        await CreateProfileAsync();
        var pins = Enumerable.Range(1, ProfileDisplay.MaxPinnedItems + 1).Select(i => new UpdateProfilePinRequest($"a{i}", null)).ToList();

        var result = await CreateController().Update(Choices(pins: pins), default);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _store.GetByMemberKeyAsync(_memberKey))!.PinnedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task A_reason_over_the_limit_is_rejected()
    {
        await CreateProfileAsync();

        var result = await CreateController().Update(
            Choices(pins: [new UpdateProfilePinRequest("b1", new string('a', ProfileDisplay.MaxPinReasonLength + 1))]),
            default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Saving_when_signed_out_is_unauthorized()
    {
        await CreateProfileAsync();

        var result = await CreateController(signedIn: false).Update(Choices(hiddenItemIds: ["b1"]), default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Members_outside_early_access_cannot_save_choices()
    {
        await CreateProfileAsync();

        var result = await CreateController(inEarlyAccess: false).Update(Choices(hiddenItemIds: ["b1"]), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await _store.GetByMemberKeyAsync(_memberKey))!.HiddenActivityIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Members_outside_early_access_cannot_read_choices()
    {
        await CreateProfileAsync();

        var result = await CreateController(inEarlyAccess: false).Get(default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Saving_without_a_profile_is_rejected()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var result = await CreateController().Update(Choices(hiddenItemIds: ["b1"]), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Reading_lists_every_section_with_hidden_items_included_and_the_stored_choices()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        await controller.Update(Choices(hiddenSections: [PlatformActivityGroups.Videos], hiddenItemIds: ["b1"], pins: [new UpdateProfilePinRequest("p1", "Proud")]), default);

        var response = (await controller.Get(default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ProfileDisplayResponse>().Subject;

        response.Sections.Select(s => s.Key).Should().Equal(PlatformActivityGroups.All);
        response.Sections.Single(s => s.Key == PlatformActivityGroups.Blog).Items.Select(i => i.Id).Should().Equal("b1", "b2");
        response.Sections.Single(s => s.Key == PlatformActivityGroups.Videos).Items.Should().ContainSingle();
        response.Choices.HiddenSections.Should().Equal(PlatformActivityGroups.Videos);
        response.Choices.HiddenItemIds.Should().Equal("b1");
        response.Choices.Pins.Should().Equal(new ProfilePinResponse("p1", "Proud", "A package"));
    }

    [Fact]
    public async Task Reading_before_onboarding_is_complete_is_rejected()
    {
        await CreateProfileAsync(completeOnboarding: false);

        var result = await CreateController().Get(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task The_graph_and_card_choices_are_stored_and_read_back()
    {
        await CreateProfileAsync();
        var controller = CreateController();

        var saved = Saved(await controller.Update(
            new UpdateProfileDisplayRequest([], [], [], HideActivityGraph: true, HideTimelineCards: true, NoCardItemIds: ["b1"]),
            default));

        saved.HideActivityGraph.Should().BeTrue();
        saved.HideTimelineCards.Should().BeTrue();
        saved.NoCardItemIds.Should().Equal("b1");

        var response = (await controller.Get(default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ProfileDisplayResponse>().Subject;
        response.Choices.HideActivityGraph.Should().BeTrue();
        response.Choices.HideTimelineCards.Should().BeTrue();
        response.Choices.NoCardItemIds.Should().Equal("b1");

        var profile = await PublicProfileAsync();
        profile.DisplayChoices.HideActivityGraph.Should().BeTrue();
        profile.DisplayChoices.HideTimelineCards.Should().BeTrue();
        profile.DisplayChoices.NoCardActivityIds.Should().Equal("b1");
    }

    [Fact]
    public async Task Saving_without_the_new_choices_switches_the_graph_and_cards_back_on()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        await controller.Update(new UpdateProfileDisplayRequest([], [], [], HideActivityGraph: true, HideTimelineCards: true, NoCardItemIds: ["b1"]), default);

        var saved = Saved(await controller.Update(Choices(), default));

        saved.HideActivityGraph.Should().BeFalse();
        saved.HideTimelineCards.Should().BeFalse();
        saved.NoCardItemIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Members_outside_early_access_cannot_change_the_graph_or_cards()
    {
        await CreateProfileAsync();

        var result = await CreateController(inEarlyAccess: false)
            .Update(new UpdateProfileDisplayRequest([], [], [], HideActivityGraph: true), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await _store.GetByMemberKeyAsync(_memberKey))!.HideActivityGraph.Should().BeFalse();
    }

    [Fact]
    public async Task The_migration_leaves_existing_profiles_with_the_graph_and_cards_on()
    {
        await using (var context = _database.CreateDbContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20261109131255_AddMemberActivityEntries");

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO MemberProfiles (MemberKey, GitHubHandle, DisplayName, OnboardingStatus, IsProfileVisible, HiddenSections, HiddenActivityIds, CreatedUtc, UpdatedUtc) " +
                "VALUES ({0}, {1}, {2}, {3}, 1, '[]', '[\"b1\"]', {4}, {5})",
                _memberKey, Handle, "The Octocat", (int)OnboardingStatus.Completed, DateTime.UtcNow, DateTime.UtcNow);

            await context.Database.MigrateAsync();
        }

        var entity = await _store.GetByMemberKeyAsync(_memberKey);
        entity!.HideActivityGraph.Should().BeFalse();
        entity.HideTimelineCards.Should().BeFalse();
        entity.NoCardActivityIds.Should().BeEmpty();
        entity.HiddenActivityIds.Should().Equal("b1");

        var choices = ProfileDisplay.StoredChoices(entity);
        choices.HideActivityGraph.Should().BeFalse();
        choices.HideTimelineCards.Should().BeFalse();
    }
}

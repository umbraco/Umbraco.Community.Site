using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Mvp.Models;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Moq;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileActivityServiceTests
{
    private const string Handle = "octocat";

    private static readonly CommunityProfile Profile = new(
        Slug: Handle,
        PlatformId: null,
        Identity: new ProfileIdentity("The Octocat", null, null, null, null, null, false, [], Handle, null, [], false),
        BlogPosts: [], Videos: [], ForumPosts: [], Packages: [], Talks: [], Badges: [], SocialPosts: [], Meetups: []);

    private static ProfileActivityService CreateService(
        FakeSyncClient client,
        ProfileActivityOptions? options = null,
        IMvpDataService? mvp = null,
        ProfileActivityEntryService? activityEntryService = null,
        MemberProfilesTestDatabase? database = null,
        IPackageFirstVersionLookup? firstVersions = null)
    {
        database ??= CreateDatabase();
        return new(
            client,
            activityEntryService ?? CreateActivityEntryService(client, database),
            new ProfileMatchStore(database),
            mvp ?? Listed(),
            new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            new TestOptionsMonitor(options ?? new ProfileActivityOptions()),
            NullLogger<ProfileActivityService>.Instance,
            firstVersions);
    }

    private sealed class FakeFirstVersions(Dictionary<string, string> firstVersions) : IPackageFirstVersionLookup
    {
        public Task<string?> GetFirstVersionAsync(string packageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(firstVersions.GetValueOrDefault(packageId));
    }

    [Fact]
    public async Task A_packages_first_release_is_marked_new_and_later_releases_are_not()
    {
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities =
            {
                [PlatformActivityGroups.Packages] =
                [
                    Activity("Released New Thing v1.0.0", "https://www.nuget.org/packages/New.Thing/1.0.0", DateTimeOffset.UtcNow, "umbraco-marketplace:package:version_released"),
                    Activity("Released Old Thing v5.5.2", "https://www.nuget.org/packages/Old.Thing/5.5.2", DateTimeOffset.UtcNow.AddDays(-1), "umbraco-marketplace:package:version_released"),
                    Activity("Released Old Thing v5.5.1", "https://www.nuget.org/packages/Old.Thing/5.5.1", DateTimeOffset.UtcNow.AddDays(-2), "umbraco-marketplace:package:version_released"),
                ],
            },
        };
        var firstVersions = new FakeFirstVersions(new() { ["new.thing"] = "1.0.0", ["old.thing"] = "2.0.0" });

        var result = await CreateService(client, firstVersions: firstVersions).EnrichAsync(Profile);

        result.Packages.Select(p => p.IsNew).Should().Equal(true, false, false);
    }

    [Fact]
    public async Task Counts_cover_the_whole_window_not_just_the_items_shown()
    {
        // Ten items are shown (the limit); twenty-five exist in the window.
        var now = DateTimeOffset.UtcNow;
        var everything = Enumerable.Range(0, 25)
            .Select(i => Activity("Post " + i, "https://blog.example/" + i, now.AddDays(-i), "blog"))
            .ToList();
        var client = new FakeSyncClient { Profile = new PlatformProfile("p1", []) };
        client.Activities[PlatformActivityGroups.Blog] = everything.Take(10).ToList();
        client.Since[PlatformActivityGroups.Blog] = everything;

        var result = await CreateService(client).EnrichAsync(Profile);

        result.BlogPosts.Should().HaveCount(10);
        result.ActivityCounts![PlatformActivityGroups.Blog].Should().Be(25);
    }

    [Fact]
    public async Task A_section_that_was_not_cut_off_is_counted_from_what_was_fetched()
    {
        var now = DateTimeOffset.UtcNow;
        var client = new FakeSyncClient { Profile = new PlatformProfile("p1", []) };
        client.Activities[PlatformActivityGroups.Blog] = [Activity("One", "https://blog.example/1", now, "blog"), Activity("Old", "https://blog.example/2", now.AddYears(-3), "blog")];
        client.Since[PlatformActivityGroups.Blog] = Enumerable.Range(0, 99).Select(i => Activity("x" + i, "https://x.example/" + i, now, "blog")).ToList();

        var result = await CreateService(client).EnrichAsync(Profile);

        // Fewer than the limit came back, so that is everything; the older item is outside the window.
        result.ActivityCounts![PlatformActivityGroups.Blog].Should().Be(1);
    }

    internal static MemberProfilesTestDatabase CreateDatabase()
    {
        var database = new MemberProfilesTestDatabase();
        using (var context = database.CreateDbContext())
        {
            context.Database.Migrate();
        }

        return database;
    }

    private static ProfileActivityEntryService CreateActivityEntryService(IProfileSyncClient client, MemberProfilesTestDatabase database)
    {
        return new ProfileActivityEntryService(
            new MemberActivityEntryStore(database),
            client,
            new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            NullLogger<ProfileActivityEntryService>.Instance);
    }

    private static PlatformActivity Activity(string title, string url, DateTimeOffset at, string type = "x:y") =>
        new(Guid.NewGuid().ToString(), type, "web", title, url, null, null, at);

    [Fact]
    public async Task Fills_the_sections_newest_first()
    {
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities =
            {
                [PlatformActivityGroups.Blog] =
                [
                    Activity("Older", "https://example.dev/a", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                    Activity("Newer", "https://example.dev/b", new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)),
                ],
                [PlatformActivityGroups.Forum] = [Activity("A thread", "https://forum.example/t/1", DateTimeOffset.UtcNow)],
                [PlatformActivityGroups.Social] = [Activity("A post", "https://social.example/1", DateTimeOffset.UtcNow)],
            },
        };

        var result = await CreateService(client).EnrichAsync(Profile);

        result.BlogPosts.Select(p => p.Title).Should().Equal("Newer", "Older");
        result.ForumPosts.Should().ContainSingle().Which.Title.Should().Be("A thread");
        result.SocialPosts.Should().ContainSingle().Which.Text.Should().Be("A post");
        result.Identity.Should().Be(Profile.Identity);
    }

    [Fact]
    public async Task Rsvps_and_attendances_both_show_as_signed_up()
    {
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities =
            {
                [PlatformActivityGroups.Meetups] =
                [
                    Activity("UmbLeeds", "https://meetup.example/1", DateTimeOffset.UtcNow, "meetup:attended"),
                    Activity("UmbYork", "https://meetup.example/2", DateTimeOffset.UtcNow.AddDays(-1), "meetup:rsvp"),
                    Activity("UmbLeeds", "https://meetup.example/1", DateTimeOffset.UtcNow.AddDays(-2), "meetup:rsvp"),
                ],
            },
        };

        var result = await CreateService(client).EnrichAsync(Profile);

        result.Meetups.Select(m => m.Action).Should().Equal("Signed up", "Signed up");
    }

    [Fact]
    public async Task Forum_counts_and_solved_answers_are_added_and_solutions_are_flagged()
    {
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            ForumStats = new PlatformForumStats(13, 31, 2),
            Activities =
            {
                [PlatformActivityGroups.Forum] =
                [
                    Activity("A topic", "https://forum.example/t/1", DateTimeOffset.UtcNow, "discourse:topic:created"),
                    Activity("Accepted answer in: Other", "https://forum.example/t/2/2", DateTimeOffset.UtcNow.AddDays(-1), "discourse:solution:accepted"),
                ],
            },
        };

        var result = await CreateService(client).EnrichAsync(Profile);

        result.ForumStats.Should().Be(new PlatformForumStats(13, 31, 2));
        result.ForumPosts.Select(p => p.IsSolution).Should().Equal(false, true);
    }

    [Fact]
    public async Task Speaking_and_hosting_win_over_signing_up_for_the_same_event()
    {
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities =
            {
                [PlatformActivityGroups.Meetups] =
                [
                    Activity("Talk night", "https://meetup.example/1", DateTimeOffset.UtcNow, "meetup:event:rsvped"),
                    Activity("Talk night", "https://meetup.example/1", DateTimeOffset.UtcNow.AddDays(-1), "meetup:event:spoke"),
                    Activity("Hack night", "https://meetup.example/2", DateTimeOffset.UtcNow.AddDays(-2), "meetup:event:hosted"),
                    Activity("Hack night", "https://meetup.example/2", DateTimeOffset.UtcNow.AddDays(-2), "meetup:event:attended"),
                ],
            },
        };

        var result = await CreateService(client).EnrichAsync(Profile);

        result.Meetups.Select(m => (m.EventName, m.Action)).Should().Equal(("Talk night", "Spoke at"), ("Hack night", "Hosted"));
    }

    [Fact]
    public async Task Items_without_a_title_or_a_web_address_are_dropped()
    {
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities =
            {
                [PlatformActivityGroups.Blog] =
                [
                    Activity("Good", "https://example.dev/a", DateTimeOffset.UtcNow),
                    Activity("Script", "javascript:alert(1)", DateTimeOffset.UtcNow),
                    Activity(" ", "https://example.dev/c", DateTimeOffset.UtcNow),
                ],
            },
        };

        var result = await CreateService(client).EnrichAsync(Profile);

        result.BlogPosts.Should().ContainSingle().Which.Title.Should().Be("Good");
    }

    [Fact]
    public async Task Every_item_carries_its_platform_activity_id()
    {
        PlatformActivity At(string id, string title) =>
            new(id, "x:y", "web", title, $"https://example.dev/{id}", null, null, DateTimeOffset.UtcNow);
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities =
            {
                [PlatformActivityGroups.Blog] = [At("b1", "Post")],
                [PlatformActivityGroups.Videos] = [At("v1", "Video")],
                [PlatformActivityGroups.Forum] = [At("f1", "Thread")],
                [PlatformActivityGroups.Packages] = [At("p1", "Package")],
                [PlatformActivityGroups.Social] = [At("s1", "Social post")],
                [PlatformActivityGroups.Meetups] = [At("m1", "Meet-up")],
            },
        };

        var result = await CreateService(client).EnrichAsync(Profile);

        result.BlogPosts.Single().ActivityId.Should().Be("b1");
        result.Videos.Single().ActivityId.Should().Be("v1");
        result.ForumPosts.Single().ActivityId.Should().Be("f1");
        result.Packages.Single().ActivityId.Should().Be("p1");
        result.SocialPosts.Single().ActivityId.Should().Be("s1");
        result.Meetups.Single().ActivityId.Should().Be("m1");
    }

    [Fact]
    public async Task Mvp_years_come_from_the_platform_when_it_reports_them()
    {
        var listed = Listed(new MvpYear(2026, [Member(Handle)]));
        var client = new FakeSyncClient { Profile = new PlatformProfile("p1", [2023, 2027]) };

        var result = await CreateService(client, mvp: listed).EnrichAsync(Profile);

        result.Identity.MvpYears.Should().Equal(2027, 2023);
        result.Identity.IsCurrentMvp.Should().BeTrue();
    }

    [Fact]
    public async Task Mvp_years_fall_back_to_the_list_when_the_platform_has_none_or_is_down()
    {
        var listed = Listed(new MvpYear(2025, [Member(Handle)]), new MvpYear(2026, [Member(Handle)]));

        var noYears = await CreateService(new FakeSyncClient { Profile = new PlatformProfile("p1", []) }, mvp: listed).EnrichAsync(Profile);
        var down = await CreateService(new FakeSyncClient { Throws = true }, mvp: listed).EnrichAsync(Profile);

        noYears.Identity.MvpYears.Should().Equal(2026, 2025);
        down.Identity.MvpYears.Should().Equal(2026, 2025);
        down.Identity.IsMvp.Should().BeTrue();
    }

    [Fact]
    public async Task Activity_from_a_rejected_source_stops_showing_at_once_but_hand_added_activity_stays()
    {
        var database = CreateDatabase();
        var profile = await new MemberProfileStore(database).GetOrStartOnboardingAsync(Guid.NewGuid(), Handle, "The Octocat");
        var entries = new MemberActivityEntryStore(database);
        var handAdded = await entries.AddAsync(profile.Id, new AddActivityEntryRequest("blog", "My own post", "https://not-mine.example/my-own-post", new DateOnly(2026, 1, 1), null));
        await entries.DecideAsync(handAdded!.Id, verify: true, null, null, "Admin");

        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities =
            {
                [PlatformActivityGroups.Blog] =
                [
                    Activity("Mine", "https://octo.example/mine", DateTimeOffset.UtcNow),
                    Activity("Not mine", "https://not-mine.example/a-post", DateTimeOffset.UtcNow),
                ],
                [PlatformActivityGroups.Social] = [Activity("Not my toot", "https://social.example/@someone/1", DateTimeOffset.UtcNow)],
                [PlatformActivityGroups.Meetups] = [Activity("Not my meetup", "https://www.not-mine.example/events/1", DateTimeOffset.UtcNow)],
            },
        };
        var service = CreateService(client, database: database);
        (await service.EnrichAsync(Profile)).BlogPosts.Should().HaveCount(3);

        var matches = new ProfileMatchStore(database);
        await matches.RecordMemberRejectionAsync(profile.Id, "https://not-mine.example", []);
        await matches.RecordMemberRejectionAsync(profile.Id, "https://social.example/@someone", []);

        var result = await service.EnrichAsync(Profile);

        result.BlogPosts.Select(p => p.Title).Should().BeEquivalentTo("Mine", "My own post");
        result.SocialPosts.Should().BeEmpty();
        result.Meetups.Should().BeEmpty();
        client.LookupCount.Should().Be(1);
    }

    [Fact]
    public async Task Held_and_confirmed_sources_do_not_hide_activity()
    {
        var database = CreateDatabase();
        var profile = await new MemberProfileStore(database).GetOrStartOnboardingAsync(Guid.NewGuid(), Handle, "The Octocat");
        var matches = new ProfileMatchStore(database);
        await matches.RecordAutomaticAsync(profile.Id, "https://octo.example", ProfileMatchState.Held, 0.3, []);
        var client = new FakeSyncClient
        {
            Profile = new PlatformProfile("p1", []),
            Activities = { [PlatformActivityGroups.Blog] = [Activity("Mine", "https://octo.example/mine", DateTimeOffset.UtcNow)] },
        };

        var result = await CreateService(client, database: database).EnrichAsync(Profile);

        result.BlogPosts.Should().ContainSingle();
    }

    [Fact]
    public void Rejected_sources_filter_every_platform_section()
    {
        var at = DateTimeOffset.UtcNow;
        var profile = Profile with
        {
            BlogPosts = [new ProfileBlogPost("b", "https://x.example/b", at, null)],
            Videos = [new ProfileVideo("v", "https://x.example/v", null, at)],
            ForumPosts = [new ProfileForumPost("f", "https://x.example/f", at, null)],
            Packages = [new ProfilePackage("p", "https://x.example/p", [])],
            SocialPosts = [new ProfileSocialPost("s", "https://x.example/s", "s", at)],
            Meetups = [new ProfileMeetup("Attended", "m", null, at, "https://x.example/m"), new ProfileMeetup("Attended", "no link", null, at)],
        };

        var result = ProfileActivityService.WithoutRejectedSources(profile, ["https://x.example"]);

        result.BlogPosts.Should().BeEmpty();
        result.Videos.Should().BeEmpty();
        result.ForumPosts.Should().BeEmpty();
        result.Packages.Should().BeEmpty();
        result.SocialPosts.Should().BeEmpty();
        result.Meetups.Should().ContainSingle().Which.EventName.Should().Be("no link");
    }

    private static IMvpDataService Listed(params MvpYear[] years)
    {
        var mvp = new Mock<IMvpDataService>();
        mvp.Setup(m => m.GetAll()).Returns(years);
        return mvp.Object;
    }

    private static MvpMember Member(string handle) => new(handle, false, "https://example.dev/a.png", null, handle);

    [Fact]
    public async Task A_member_the_platform_does_not_know_gets_the_profile_unchanged()
    {
        var result = await CreateService(new FakeSyncClient { Profile = null }).EnrichAsync(Profile);

        result.Should().BeSameAs(Profile);
    }

    [Fact]
    public async Task A_platform_failure_gets_the_profile_unchanged()
    {
        var result = await CreateService(new FakeSyncClient { Throws = true }).EnrichAsync(Profile);

        result.Should().BeSameAs(Profile);
    }

    [Fact]
    public async Task The_answer_is_cached_per_handle_ignoring_case()
    {
        var client = new FakeSyncClient { Profile = new PlatformProfile("p1", []) };
        var service = CreateService(client);

        await service.EnrichAsync(Profile);
        await service.EnrichAsync(Profile with { Slug = "OctoCat" });

        client.LookupCount.Should().Be(1);
    }

    [Fact]
    public async Task A_failure_is_cached_too_so_the_platform_is_not_hit_on_every_view()
    {
        var client = new FakeSyncClient { Throws = true };
        var service = CreateService(client);

        await service.EnrichAsync(Profile);
        await service.EnrichAsync(Profile);

        client.LookupCount.Should().Be(1);
    }

    [Fact]
    public async Task Asks_for_no_more_items_than_the_section_limit()
    {
        var client = new FakeSyncClient { Profile = new PlatformProfile("p1", []) };

        await CreateService(client, new ProfileActivityOptions { ItemsPerSection = 3 }).EnrichAsync(Profile);

        client.RequestedLimits.Should().OnlyContain(l => l == 3);
        client.RequestedGroups.Should().BeEquivalentTo(PlatformActivityGroups.FromPlatform);
    }

    private sealed class FakeSyncClient : IProfileSyncClient
    {
        public PlatformProfile? Profile { get; init; }
        public bool Throws { get; init; }
        public Dictionary<string, IReadOnlyList<PlatformActivity>> Activities { get; } = new();
        public PlatformForumStats? ForumStats { get; init; }
        public int LookupCount { get; private set; }
        public List<int> RequestedLimits { get; } = [];
        public List<string> RequestedGroups { get; } = [];

        public Task<PlatformProfile?> FindProfileAsync(string handle, CancellationToken cancellationToken = default)
        {
            LookupCount++;
            return Throws ? throw new HttpRequestException("simulated") : Task.FromResult(Profile);
        }

        public Task<IReadOnlyList<PlatformActivity>> GetActivitiesAsync(string platformProfileId, string group, int limit, CancellationToken cancellationToken = default)
        {
            lock (RequestedGroups)
            {
                RequestedGroups.Add(group);
                RequestedLimits.Add(limit);
            }

            return Task.FromResult(Activities.TryGetValue(group, out var items) ? items : []);
        }

        /// <summary>What the paged "everything since" fetch returns, by group; the newest items when not set.</summary>
        public Dictionary<string, IReadOnlyList<PlatformActivity>> Since { get; } = new();

        public Task<IReadOnlyList<PlatformActivity>> GetActivitiesSinceAsync(string platformProfileId, string group, DateOnly from, int maxItems, CancellationToken cancellationToken = default) =>
            Task.FromResult(Since.TryGetValue(group, out var items) ? items : Activities.TryGetValue(group, out var all) ? all : []);

        public Task<PlatformForumStats?> GetForumStatsAsync(string platformProfileId, int months, CancellationToken cancellationToken = default) =>
            Task.FromResult(ForumStats);

        public Task<IReadOnlyList<PlatformLinkCheck>> VerifyLinksAsync(string handle, IReadOnlyList<string> urls, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlatformLinkCheck>>([]);
        public Task NotifyProfileClaimedAsync(string handle, string? platformProfileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyActivityVerifiedAsync(string handle, ActivityEntryRecord entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyActivityRemovedAsync(string handle, string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);
    }

    private sealed class TestOptionsMonitor(ProfileActivityOptions value) : IOptionsMonitor<ProfileActivityOptions>
    {
        public ProfileActivityOptions CurrentValue => value;
        public ProfileActivityOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ProfileActivityOptions, string?> listener) => null;
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Mvp.Models;
using UmbracoCommunity.Web.Features.Profiles;
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

    private static ProfileActivityService CreateService(FakeSyncClient client, ProfileActivityOptions? options = null, IMvpDataService? mvp = null) =>
        new(
            client,
            mvp ?? Listed(),
            TestImageProxy.Create(),
            new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)),
            new TestOptionsMonitor(options ?? new ProfileActivityOptions()),
            NullLogger<ProfileActivityService>.Instance);

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
    public async Task Attended_meetups_show_as_attended()
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
                ],
            },
        };

        var result = await CreateService(client).EnrichAsync(Profile);

        result.Meetups.Select(m => m.Action).Should().Equal("Attended", "RSVP'd yes to");
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
        client.RequestedGroups.Should().BeEquivalentTo(PlatformActivityGroups.All);
    }

    private sealed class FakeSyncClient : IProfileSyncClient
    {
        public PlatformProfile? Profile { get; init; }
        public bool Throws { get; init; }
        public Dictionary<string, IReadOnlyList<PlatformActivity>> Activities { get; } = new();
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

        public Task NotifyProfileClaimedAsync(string handle, string? platformProfileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);
    }

    private sealed class TestOptionsMonitor(ProfileActivityOptions value) : IOptionsMonitor<ProfileActivityOptions>
    {
        public ProfileActivityOptions CurrentValue => value;
        public ProfileActivityOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ProfileActivityOptions, string?> listener) => null;
    }
}

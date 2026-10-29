using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class MemberProfileLinkTests : IDisposable
{
    private const string Handle = "octocat";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly MemberProfileDataProvider _provider;

    public MemberProfileLinkTests()
    {
        _store = new MemberProfileStore(_database);
        _provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>()),
            TestImageProxy.Create());
    }

    public void Dispose() => _database.Dispose();

    private async Task<Guid> CreateVisibleProfileAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var memberKey = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(memberKey, Handle, "The Octocat");
        await _store.CompleteOnboardingAsync(memberKey);
        await _store.SetProfileVisibleAsync(memberKey, true);
        return memberKey;
    }

    private async Task<List<int>> AddLinksAsync(Guid memberKey, params string[] platforms)
    {
        var ids = new List<int>();
        foreach (var platform in platforms)
        {
            ids.Add((await _store.AddFeedAsync(memberKey, platform, $"https://example.org/{platform}"))!.Id);
        }

        return ids;
    }

    private async Task<string[]> PlatformsAsync(Guid memberKey) =>
        (await _store.GetActiveFeedsAsync(memberKey)).Select(f => f.Platform).ToArray();

    [Fact]
    public async Task New_links_go_to_the_end_of_the_list()
    {
        var memberKey = await CreateVisibleProfileAsync();

        await AddLinksAsync(memberKey, "Blog", "Mastodon", "LinkedIn");

        (await PlatformsAsync(memberKey)).Should().Equal("Blog", "Mastodon", "LinkedIn");
    }

    [Fact]
    public async Task Links_from_before_ordering_existed_keep_their_date_added_order_and_new_ones_follow()
    {
        var memberKey = await CreateVisibleProfileAsync();
        var ids = await AddLinksAsync(memberKey, "Old1", "Old2");
        await using (var context = _database.CreateDbContext())
        {
            // What the migration leaves behind: every existing row at 0, in the order it was added.
            await context.MemberFeeds.ExecuteUpdateAsync(s => s.SetProperty(f => f.SortOrder, 0));
            var first = await context.MemberFeeds.FirstAsync(f => f.Id == ids[0]);
            first.AddedUtc = DateTime.UtcNow.AddDays(-2);
            await context.SaveChangesAsync();
        }

        await AddLinksAsync(memberKey, "New");

        (await PlatformsAsync(memberKey)).Should().Equal("Old1", "Old2", "New");
    }

    [Fact]
    public async Task A_reorder_persists_and_shows_on_the_public_profile()
    {
        var memberKey = await CreateVisibleProfileAsync();
        var ids = await AddLinksAsync(memberKey, "Blog", "Mastodon", "LinkedIn");

        (await _store.ReorderFeedsAsync(memberKey, [ids[2], ids[0], ids[1]])).Should().BeTrue();

        (await PlatformsAsync(memberKey)).Should().Equal("LinkedIn", "Blog", "Mastodon");
        (await _provider.GetProfileAsync(Handle))!.Identity.SocialLinks.Select(l => l.Platform)
            .Should().Equal("LinkedIn", "Blog", "Mastodon");
    }

    [Fact]
    public async Task A_reorder_that_does_not_name_exactly_the_active_links_is_refused()
    {
        var memberKey = await CreateVisibleProfileAsync();
        var ids = await AddLinksAsync(memberKey, "Blog", "Mastodon");

        (await _store.ReorderFeedsAsync(memberKey, [ids[0]])).Should().BeFalse();
        (await _store.ReorderFeedsAsync(memberKey, [ids[0], ids[0]])).Should().BeFalse();
        (await _store.ReorderFeedsAsync(memberKey, [ids[0], 9999])).Should().BeFalse();
        (await PlatformsAsync(memberKey)).Should().Equal("Blog", "Mastodon");
    }

    [Fact]
    public async Task Another_members_links_cannot_be_reordered_or_edited()
    {
        var memberKey = await CreateVisibleProfileAsync();
        var ids = await AddLinksAsync(memberKey, "Blog");
        var stranger = Guid.NewGuid();
        await _store.GetOrStartOnboardingAsync(stranger, "stranger", "Stranger");

        (await _store.ReorderFeedsAsync(stranger, ids)).Should().BeFalse();
        (await _store.UpdateFeedAsync(stranger, ids[0], "Mine", "https://example.org/mine", null)).Should().BeNull();
    }

    [Fact]
    public async Task Editing_a_link_changes_it_and_a_new_address_resets_sync_and_preview()
    {
        var memberKey = await CreateVisibleProfileAsync();
        var id = (await AddLinksAsync(memberKey, "Blog"))[0];
        await _store.MarkFeedPlatformSyncResultAsync(id, succeeded: true, error: null);
        var preview = new LinkPreview(LinkPreviewStatus.Available, "Title", "https://example.org/c.png", "https://example.org/f.ico");

        var updated = await _store.UpdateFeedAsync(memberKey, id, "My blog", "https://example.org/new", preview);

        updated!.Platform.Should().Be("My blog");
        updated.Url.Should().Be("https://example.org/new");
        updated.PlatformSyncStatus.Should().Be(FeedSyncStatus.Pending);
        updated.PreviewImageUrl.Should().Be("https://example.org/c.png");
        updated.PreviewStatus.Should().Be(LinkPreviewStatus.Available);
    }

    [Fact]
    public async Task Renaming_without_changing_the_address_keeps_sync_state_and_preview()
    {
        var memberKey = await CreateVisibleProfileAsync();
        var id = (await AddLinksAsync(memberKey, "Blog"))[0];
        await _store.MarkFeedPlatformSyncResultAsync(id, succeeded: true, error: null);

        var updated = await _store.UpdateFeedAsync(memberKey, id, "Renamed", "https://example.org/Blog", null);

        updated!.Platform.Should().Be("Renamed");
        updated.PlatformSyncStatus.Should().Be(FeedSyncStatus.Synced);
    }

    [Fact]
    public async Task The_public_profile_shows_a_links_favicon_through_the_image_proxy_and_never_the_raw_address()
    {
        var memberKey = await CreateVisibleProfileAsync();
        await _store.AddFeedAsync(
            memberKey, "GitHub", "https://github.com/umbraco",
            new LinkPreview(LinkPreviewStatus.Available, null, null, "https://github.com/favicon.ico"));
        await _store.AddFeedAsync(memberKey, "Blog", "https://example.org/blog");

        var links = (await _provider.GetProfileAsync(Handle))!.Identity.SocialLinks;

        links[0].FaviconUrl.Should().StartWith("/api/feed-submission/image-proxy?token=").And.NotContain("github.com");
        links[1].FaviconUrl.Should().BeNull();
    }
}

using UmbracoCommunity.BlogAnnouncements;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Bridges the blog-announcements dashboard's "Poll now" action to the community-blogs refresh.
/// Runs the exact same full cycle as <see cref="CommunityBlogsBackgroundService"/>'s timer:
/// Sphere fetch, announcement detection/delivery, image mirroring, cache + search-index refresh.
/// Overlap with a timer-driven cycle is serialised inside <see cref="CommunityBlogsService"/>.
/// </summary>
public sealed class CommunityBlogsFeedPoller : ICommunityFeedPoller
{
    private readonly ICommunityBlogsService _service;

    public CommunityBlogsFeedPoller(ICommunityBlogsService service) => _service = service;

    public Task PollNowAsync(CancellationToken cancellationToken) => _service.RefreshAsync(cancellationToken);
}

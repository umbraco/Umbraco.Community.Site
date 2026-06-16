namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public interface ICommunityBlogsService
{
    /// <summary>Re-aggregates posts and persists them (memory + disk). No-ops if nothing could be fetched.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the current data from memory, falling back to the disk cache, then stale, then empty.</summary>
    CommunityBlogsData GetData();

    /// <summary>Returns one page of posts (1-based, clamped to the valid range).</summary>
    PagedCommunityBlogPosts GetPage(int page, int pageSize);
}

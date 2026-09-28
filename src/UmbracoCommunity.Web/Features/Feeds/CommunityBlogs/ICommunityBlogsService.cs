namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public interface ICommunityBlogsService
{
    /// <summary>Re-aggregates posts and persists them (memory + disk). No-ops if nothing could be fetched.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the current data from memory, falling back to the disk cache, then stale, then empty.</summary>
    CommunityBlogsData GetData();

    /// <summary>
    /// Returns one page of posts for reader-facing listings (1-based, clamped to the valid range),
    /// leaving out posts whose URL host is in <see cref="CommunityBlogsOptions.ExcludedListingHosts"/>.
    /// Filtering happens before paging, so totals and page counts reflect the filtered set. Use
    /// <see cref="GetData"/> for the unfiltered posts.
    /// </summary>
    PagedCommunityBlogPosts GetPage(int page, int pageSize);

    /// <summary>True when <paramref name="url"/>'s host is in <see cref="CommunityBlogsOptions.ExcludedListingHosts"/>.</summary>
    bool IsExcludedFromListings(string? url);
}

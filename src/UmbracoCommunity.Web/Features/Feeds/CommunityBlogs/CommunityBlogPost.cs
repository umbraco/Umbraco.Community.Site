namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>A community blog post, mapped from the external content API for rendering.</summary>
public sealed record CommunityBlogPost(
    string Id,
    string Title,
    string Url,
    string? Excerpt,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    string? AuthorName,
    string? AuthorAvatarUrl,
    string? AuthorProfileUrl);

/// <summary>The aggregated, ordered set of posts plus when it was built.</summary>
public sealed record CommunityBlogsData(
    IReadOnlyList<CommunityBlogPost> Posts,
    DateTimeOffset LastUpdatedUtc)
{
    public static CommunityBlogsData Empty { get; } =
        new(Array.Empty<CommunityBlogPost>(), DateTimeOffset.MinValue);
}

/// <summary>One page of posts for the listing view.</summary>
public sealed record PagedCommunityBlogPosts(
    IReadOnlyList<CommunityBlogPost> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

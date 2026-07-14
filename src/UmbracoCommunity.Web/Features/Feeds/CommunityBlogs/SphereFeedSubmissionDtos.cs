namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

// The POST blog-posts/preview endpoint returns the same envelope as GET blog-posts,
// so PreviewFeedAsync reuses the existing PostsResponseDto rather than a bespoke record.

/// <summary>Response from PUT /v1/feed-submissions (bare, unwrapped object).</summary>
public sealed record FeedSubmissionResponseDto(
    string? Id,
    string Url,
    string? Name,
    string? Github,
    string Status,
    DateTimeOffset? SubmittedAt);

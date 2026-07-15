namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs.Models;

/// <summary>Response body for the preview endpoint: the rendered cards plus the feed's current submission status.</summary>
public sealed record FeedPreviewResponse(IReadOnlyList<PublicPostDto> Posts, string Status);

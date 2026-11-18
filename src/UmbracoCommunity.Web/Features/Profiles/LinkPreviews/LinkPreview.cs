namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

/// <summary>What the server learned about a member's link. Image URLs are absolute https URLs on the linked site.</summary>
/// <param name="Description">The page's own short description of itself, when it gives one.</param>
/// <param name="PublishedDate">When the page says it was published (best effort; the date as the page writes it), or null.</param>
/// <param name="PageType">The page's <c>og:type</c> (e.g. <c>article</c>, <c>video.other</c>), lower case, or null.</param>
public sealed record LinkPreview(
    LinkPreviewStatus Status,
    string? Title,
    string? ImageUrl,
    string? FaviconUrl,
    string? Description = null,
    DateOnly? PublishedDate = null,
    string? PageType = null)
{
    public static LinkPreview Unreachable { get; } = new(LinkPreviewStatus.Unreachable, null, null, null);
}

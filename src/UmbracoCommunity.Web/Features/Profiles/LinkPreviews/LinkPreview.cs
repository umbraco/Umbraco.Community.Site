namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

/// <summary>What the server learned about a member's link. Image URLs are absolute https URLs on the linked site.</summary>
/// <param name="Description">The page's own short description of itself, when it gives one.</param>
public sealed record LinkPreview(LinkPreviewStatus Status, string? Title, string? ImageUrl, string? FaviconUrl, string? Description = null)
{
    public static LinkPreview Unreachable { get; } = new(LinkPreviewStatus.Unreachable, null, null, null);
}

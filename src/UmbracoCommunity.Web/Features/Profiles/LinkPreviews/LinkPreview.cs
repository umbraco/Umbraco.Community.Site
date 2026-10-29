namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

/// <summary>What the server learned about a member's link. Image URLs are absolute https URLs on the linked site.</summary>
public sealed record LinkPreview(LinkPreviewStatus Status, string? Title, string? ImageUrl, string? FaviconUrl)
{
    public static LinkPreview Unreachable { get; } = new(LinkPreviewStatus.Unreachable, null, null, null);
}

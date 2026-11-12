namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

/// <summary>
/// The picture and description a timeline entry's page offered when it was last read, kept so the entry keeps its
/// preview card when the page later changes or disappears. One row per page address. The picture itself is a copy
/// stored by <see cref="ProfileTimelineImageStore"/>, found by its source address.
/// </summary>
public class ProfileTimelinePreviewEntity
{
    public const int MaxUrlLength = 2048;
    public const int UrlHashLength = 64;
    public const int MaxDescriptionLength = 300;

    public int Id { get; set; }

    /// <summary>The SHA-256 of <see cref="PageUrl"/>, hex-encoded, for lookups (the address itself is too long to index).</summary>
    public string PageUrlHash { get; set; } = string.Empty;

    public string PageUrl { get; set; } = string.Empty;

    /// <summary>The page's share picture, an https address on the page's own site.</summary>
    public string ImageUrl { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CheckedUtc { get; set; }
}

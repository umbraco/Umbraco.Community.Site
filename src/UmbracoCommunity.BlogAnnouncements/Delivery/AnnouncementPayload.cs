namespace UmbracoCommunity.BlogAnnouncements.Delivery;

/// <summary>
/// Destination-agnostic view of a post to announce. Carries the original (remote) upstream image
/// URLs, not the site's localized copies — the destination fetches them itself.
/// </summary>
public sealed record AnnouncementPayload(
    string Title,
    string Url,
    string? Excerpt,
    string? AuthorName,
    string? AuthorProfileUrl,
    string? AvatarUrl,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt);

/// <summary>Outcome of a delivery attempt.</summary>
public sealed record DeliveryResult(bool Success, int? HttpStatus, bool DryRun)
{
    public static DeliveryResult Dry { get; } = new(Success: false, HttpStatus: null, DryRun: true);
    public static DeliveryResult Ok(int httpStatus) => new(Success: true, HttpStatus: httpStatus, DryRun: false);
    public static DeliveryResult Fail(int? httpStatus) => new(Success: false, HttpStatus: httpStatus, DryRun: false);
}

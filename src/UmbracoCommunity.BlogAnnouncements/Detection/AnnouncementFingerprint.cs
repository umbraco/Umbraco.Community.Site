using System.Globalization;
using System.Text.RegularExpressions;

namespace UmbracoCommunity.BlogAnnouncements.Detection;

/// <summary>
/// Builds the secondary dedup key: normalized author + normalized title + publish date
/// (day-granular). Catches the same post surfacing under a different domain (e.g. an Azure
/// *.azurewebsites.net URL alongside the custom domain), where the Sphere GUID differs.
/// </summary>
public static partial class AnnouncementFingerprint
{
    /// <summary>
    /// Maximum fingerprint length — mirrored by the column's HasMaxLength, capped at 450 so the
    /// indexed nvarchar column stays inside SQL Server's index key size limits.
    /// </summary>
    public const int MaxLength = 450;

    public static string Compute(string? authorName, string? title, DateTimeOffset publishedAt)
    {
        var author = Normalize(authorName);
        var normalizedTitle = Normalize(title);
        var date = publishedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fingerprint = $"{author}|{normalizedTitle}|{date}";

        // Defensive truncation for absurdly long author/title combinations, so an insert can never
        // blow the column limit. Truncation is deterministic, so dedup behaviour stays consistent.
        return fingerprint.Length <= MaxLength ? fingerprint : fingerprint[..MaxLength];
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var collapsed = WhitespaceRegex().Replace(value.Trim(), " ");
        return collapsed.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

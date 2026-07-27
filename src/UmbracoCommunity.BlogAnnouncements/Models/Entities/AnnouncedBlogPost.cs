namespace UmbracoCommunity.BlogAnnouncements.Models.Entities;

/// <summary>
/// A community blog post tracked by the announcement pipeline. Keyed on the external platform's
/// stable post GUID. Author/excerpt/image fields are denormalised so the dashboard survives posts
/// falling out of the transient upstream cache window.
/// </summary>
public class AnnouncedBlogPost
{
    /// <summary>The external platform's post GUID — the primary dedup key.</summary>
    public Guid PlatformPostId { get; set; }

    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Publish time in UTC. Stored as <see cref="DateTime"/> (not <see cref="DateTimeOffset"/>)
    /// because SQLite — this site's default provider — cannot ORDER BY / range-filter a
    /// DateTimeOffset column. Upstream publish times are effectively UTC (often bare midnight).
    /// </summary>
    public DateTime PublishedAtUtc { get; set; }

    /// <summary>Normalized author + normalized title + publish date — secondary dup guard (same post, different domain).</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>When our refresh first saw this post.</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>Null until Discord delivery is confirmed.</summary>
    public DateTime? AnnouncedUtc { get; set; }

    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Pending;

    // Denormalised post fields (for the dashboard and the Discord payload).
    public string? AuthorName { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public string? AuthorProfileUrl { get; set; }
    public string? Excerpt { get; set; }
    public string? CoverImageUrl { get; set; }

    public List<AnnouncementAttempt> Attempts { get; set; } = new();
}

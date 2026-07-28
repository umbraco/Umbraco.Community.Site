using UmbracoCommunity.BlogAnnouncements.Models.Entities;

namespace UmbracoCommunity.BlogAnnouncements.Delivery;

/// <summary>
/// The single place a tracked post is turned into an <see cref="AnnouncementPayload"/>. The
/// automatic detection cycle and the manual dashboard actions (repost / post-now) both go through
/// here so the Discord formatting can never drift between them.
/// </summary>
public static class AnnouncementPayloadFactory
{
    /// <summary>
    /// Visual cap for the embed description. Discord's hard limit is 4096, but multi-paragraph
    /// excerpts drown the channel — the announcement is a teaser, the link carries the full post.
    /// </summary>
    public const int MaxExcerptLength = 300;

    /// <summary>Builds the announcement payload for a tracked post.</summary>
    public static AnnouncementPayload FromPost(AnnouncedBlogPost post) => new(
        post.Title,
        post.Url,
        TruncateExcerpt(post.Excerpt),
        post.AuthorName,
        post.AuthorProfileUrl,
        post.AuthorAvatarUrl,
        post.CoverImageUrl,
        new DateTimeOffset(DateTime.SpecifyKind(post.PublishedAtUtc, DateTimeKind.Utc)));

    /// <summary>
    /// A canned payload for the dashboard's "Send test message" button — proves the webhook is
    /// wired without touching any tracked post.
    /// </summary>
    public static AnnouncementPayload CreateTestMessage(DateTimeOffset now) => new(
        Title: "Test message from the Umbraco Community site",
        Url: "https://community.umbraco.com",
        Excerpt: "If you can see this in Discord, the blog-announcement webhook is configured correctly.",
        AuthorName: "Umbraco Community",
        AuthorProfileUrl: "https://community.umbraco.com",
        AvatarUrl: null,
        CoverImageUrl: null,
        PublishedAt: now);

    /// <summary>
    /// Truncates a long excerpt to <see cref="MaxExcerptLength"/> characters, cutting at a word
    /// boundary where possible (so URLs/words aren't chopped mid-token), trimming trailing
    /// punctuation from the cut point, and appending an ellipsis. Short excerpts pass through
    /// unchanged; null/whitespace stays as-is.
    /// </summary>
    internal static string? TruncateExcerpt(string? excerpt)
    {
        if (string.IsNullOrWhiteSpace(excerpt) || excerpt.Length <= MaxExcerptLength)
        {
            return excerpt;
        }

        var cut = excerpt[..MaxExcerptLength];

        // Prefer the last word boundary before the cap so we never end mid-word or mid-URL.
        // If the first "word" alone exceeds the cap (no whitespace at all), keep the hard cut.
        var lastSpace = cut.LastIndexOfAny([' ', '\t', '\n', '\r']);
        if (lastSpace > 0)
        {
            cut = cut[..lastSpace];
        }

        // Drop trailing whitespace and punctuation so we don't render ",…" / ".…" / "(…".
        cut = cut.TrimEnd().TrimEnd('.', ',', ';', ':', '!', '?', '-', '–', '—', '…', '\'', '"', '(', '[', '{', '/');

        return cut + "…";
    }
}

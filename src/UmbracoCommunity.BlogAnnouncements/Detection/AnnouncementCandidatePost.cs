namespace UmbracoCommunity.BlogAnnouncements.Detection;

/// <summary>
/// A neutral, source-agnostic view of a blog post handed to the detection pipeline. Keeps this
/// project independent of the host's feed models (e.g. the site's <c>CommunityBlogPost</c>) — the
/// caller maps its own post shape onto this record at the call site. Carries the original (remote)
/// image URLs, not any localized copies, so the delivery leg can hand them straight to Discord.
/// </summary>
public sealed record AnnouncementCandidatePost(
    string Id,
    string Title,
    string Url,
    string? Excerpt,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    string? AuthorName,
    string? AuthorAvatarUrl,
    string? AuthorProfileUrl);

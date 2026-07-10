namespace UmbracoCommunity.BlogAnnouncements.Detection;

/// <summary>
/// Diffs a fresh set of candidate blog posts against the tracking store and announces the
/// new-within-window ones via the delivery leg. Invoked from the host's blog refresh cycle
/// after fresh data lands.
/// </summary>
public interface IBlogAnnouncementDetector
{
    /// <summary>
    /// Runs one detection cycle. <paramref name="posts"/> must carry the raw, pre-localization
    /// image URLs so Discord fetches the original remote ones itself.
    /// </summary>
    Task DetectAndAnnounceAsync(IReadOnlyCollection<AnnouncementCandidatePost> posts, CancellationToken cancellationToken = default);
}

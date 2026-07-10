namespace UmbracoCommunity.BlogAnnouncements.Detection;

/// <summary>Outcome of <see cref="IBlogAnnouncementDetector.PollAsync"/>.</summary>
public sealed record PollResult(int Fetched, int New, int Skipped);

/// <summary>Outcome of <see cref="IBlogAnnouncementDetector.AnnounceQueuedAsync"/>.</summary>
public sealed record AnnounceResult(int Announced, int Failed);

/// <summary>
/// Diffs a fresh set of candidate blog posts against the tracking store and announces the
/// new-within-window ones via the delivery leg.
/// </summary>
public interface IBlogAnnouncementDetector
{
    /// <summary>
    /// Runs one full detect-and-announce cycle (ingest, then deliver, then record one
    /// <c>AnnouncementRun</c> row) — equivalent to calling <see cref="PollAsync"/> followed by
    /// <see cref="AnnounceQueuedAsync"/> with its result. <paramref name="posts"/> must carry the
    /// raw, pre-localization image URLs so Discord fetches the original remote ones itself.
    /// </summary>
    Task DetectAndAnnounceAsync(IReadOnlyCollection<AnnouncementCandidatePost> posts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests a fresh set of candidate posts: records never-seen ones and refreshes tracked ones'
    /// denormalised metadata. Does not deliver or record a run — pair with
    /// <see cref="AnnounceQueuedAsync"/> to complete a cycle. Split out so Umbraco Automate can run
    /// fetch and deliver as separate, independently observable steps.
    /// </summary>
    Task<PollResult> PollAsync(IReadOnlyCollection<AnnouncementCandidatePost> posts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers the queued (Pending/Failed) posts up to the per-cycle cap, then records one
    /// <c>AnnouncementRun</c> row combining the given ingest numbers with this delivery's results.
    /// </summary>
    Task<AnnounceResult> AnnounceQueuedAsync(int fetched, int newCount, int skippedCount, CancellationToken cancellationToken = default);
}

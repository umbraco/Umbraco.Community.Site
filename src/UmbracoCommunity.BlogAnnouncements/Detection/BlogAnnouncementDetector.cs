using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using UmbracoCommunity.BlogAnnouncements.Infrastructure;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;

namespace UmbracoCommunity.BlogAnnouncements.Detection;

/// <summary>
/// Diffs fresh upstream posts against the tracking store, records never-seen posts, and delivers the
/// eligible ones via <see cref="IDiscordAnnouncer"/> — respecting the recency window and per-cycle
/// cap. Marks a post <c>Announced</c> only after delivery confirms success, so failures retry next
/// cycle. Writes one <see cref="AnnouncementRun"/> heartbeat row per cycle.
/// </summary>
public sealed class BlogAnnouncementDetector : IBlogAnnouncementDetector
{
    private readonly IDbContextFactory<BlogAnnouncementsDbContext> _contextFactory;
    private readonly IDiscordAnnouncer _announcer;
    private readonly IOptionsMonitor<BlogAnnouncementsOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<BlogAnnouncementDetector> _logger;

    public BlogAnnouncementDetector(
        IDbContextFactory<BlogAnnouncementsDbContext> contextFactory,
        IDiscordAnnouncer announcer,
        IOptionsMonitor<BlogAnnouncementsOptions> options,
        TimeProvider time,
        ILogger<BlogAnnouncementDetector> logger)
    {
        _contextFactory = contextFactory;
        _announcer = announcer;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task DetectAndAnnounceAsync(IReadOnlyCollection<AnnouncementCandidatePost> posts, CancellationToken cancellationToken = default)
    {
        var poll = await PollAsync(posts, cancellationToken);
        await AnnounceQueuedAsync(poll.Fetched, poll.New, poll.Skipped, cancellationToken);
    }

    public async Task<PollResult> PollAsync(IReadOnlyCollection<AnnouncementCandidatePost> posts, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var windowStartUtc = nowUtc - TimeSpan.FromDays(Math.Max(0, options.RecencyWindowDays));

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var (newCount, skippedCount) = await IngestNewPostsAsync(db, posts, windowStartUtc, nowUtc, cancellationToken);

        return new PollResult(posts.Count, newCount, skippedCount);
    }

    public async Task<AnnounceResult> AnnounceQueuedAsync(int fetched, int newCount, int skippedCount, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var (announcedCount, failedCount) = await DeliverQueueAsync(db, options, nowUtc, cancellationToken);

        db.AnnouncementRuns.Add(new AnnouncementRun
        {
            RunUtc = nowUtc,
            Fetched = fetched,
            New = newCount,
            Announced = announcedCount,
            Skipped = skippedCount,
            Failed = failedCount,
            DryRun = options.DryRun,
        });
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "BlogAnnouncements cycle: fetched {Fetched}, new {New}, announced {Announced}, skipped {Skipped}, failed {Failed}, dryRun {DryRun}.",
            fetched, newCount, announcedCount, skippedCount, failedCount, options.DryRun);

        return new AnnounceResult(announcedCount, failedCount);
    }

    /// <summary>
    /// Records never-seen posts as Pending (within window) or SkippedTooOld (older), and refreshes
    /// the denormalised metadata of already-tracked posts from the fresh feed (the feed is the
    /// source of truth — e.g. the platform correcting a broken avatar URL after we first saw the post).
    /// Runs before delivery, so Pending/Failed posts deliver with current data. Returns
    /// (new, skippedTooOld).
    /// </summary>
    private async Task<(int New, int Skipped)> IngestNewPostsAsync(
        BlogAnnouncementsDbContext db,
        IReadOnlyCollection<AnnouncementCandidatePost> posts,
        DateTime windowStartUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var known = await db.AnnouncedBlogPosts
            .Select(p => new { p.PlatformPostId, p.Fingerprint })
            .ToListAsync(cancellationToken);
        var knownIds = known.Select(k => k.PlatformPostId).ToHashSet();
        var knownFingerprints = known.Select(k => k.Fingerprint).ToHashSet(StringComparer.Ordinal);

        var added = new List<AnnouncedBlogPost>();
        var skippedCount = 0;
        var trackedCandidates = new List<(Guid PlatformPostId, AnnouncementCandidatePost Post)>();

        foreach (var post in posts)
        {
            if (!Guid.TryParse(post.Id, out var platformPostId))
            {
                _logger.LogWarning("Skipping community blog post with non-GUID id '{Id}'.", post.Id);
                continue;
            }

            // Already tracked by platform post id — refresh its snapshotted metadata below.
            if (knownIds.Contains(platformPostId))
            {
                trackedCandidates.Add((platformPostId, post));
                continue;
            }

            // Same post surfacing under a different domain — ignore silently (no row).
            var fingerprint = AnnouncementFingerprint.Compute(post.AuthorName, post.Title, post.PublishedAt);
            if (knownFingerprints.Contains(fingerprint))
            {
                continue;
            }

            var publishedAtUtc = post.PublishedAt.UtcDateTime;
            var withinWindow = publishedAtUtc >= windowStartUtc;
            var row = new AnnouncedBlogPost
            {
                PlatformPostId = platformPostId,
                Url = post.Url,
                Title = post.Title,
                PublishedAtUtc = publishedAtUtc,
                Fingerprint = fingerprint,
                FirstSeenUtc = nowUtc,
                Status = withinWindow ? AnnouncementStatus.Pending : AnnouncementStatus.SkippedTooOld,
                AuthorName = post.AuthorName,
                AuthorAvatarUrl = post.AuthorAvatarUrl,
                AuthorProfileUrl = post.AuthorProfileUrl,
                Excerpt = post.Excerpt,
                CoverImageUrl = post.CoverImageUrl,
            };
            db.AnnouncedBlogPosts.Add(row);
            added.Add(row);

            knownIds.Add(platformPostId);
            knownFingerprints.Add(fingerprint);
            if (!withinWindow)
            {
                skippedCount++;
            }
        }

        await RefreshTrackedMetadataAsync(db, trackedCandidates, cancellationToken);

        // EF only writes modified columns; when nothing is new and nothing changed, this is a no-op.
        var lostRaces = await SaveIngestAsync(db, added, cancellationToken);

        // Rows another cycle inserted first aren't ours to count as new (or as skipped).
        return (added.Count - lostRaces.Count,
            skippedCount - lostRaces.Count(p => p.Status == AnnouncementStatus.SkippedTooOld));
    }

    /// <summary>
    /// Commits the ingest, recovering from the one collision the read-then-insert above can lose:
    /// a concurrent poll cycle (see <see cref="AnnouncementClaims"/> for why cycles overlap)
    /// inserting the same post between our read of the known ids and this write, which the primary
    /// key then rejects. The rows the other cycle got in first are dropped and the rest committed,
    /// so a lost race costs us nothing rather than failing the whole cycle. Returns the dropped
    /// rows. Anything else — a genuine constraint or connection failure — propagates.
    /// </summary>
    private async Task<IReadOnlyCollection<AnnouncedBlogPost>> SaveIngestAsync(
        BlogAnnouncementsDbContext db,
        IReadOnlyCollection<AnnouncedBlogPost> added,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return [];
        }
        catch (DbUpdateException) when (added.Count > 0)
        {
            // Nothing was committed (EF batches the insert in one transaction), so the entities are
            // still Added and the retry below re-sends whatever survives.
            var ids = added.Select(p => p.PlatformPostId).ToList();
            var alreadyStored = await db.AnnouncedBlogPosts
                .AsNoTracking()
                .Where(p => ids.Contains(p.PlatformPostId))
                .Select(p => p.PlatformPostId)
                .ToListAsync(cancellationToken);

            if (alreadyStored.Count == 0)
            {
                throw;
            }

            var dropped = added.Where(p => alreadyStored.Contains(p.PlatformPostId)).ToList();
            foreach (var row in dropped)
            {
                db.Entry(row).State = EntityState.Detached;
            }

            _logger.LogInformation(
                "Dropped {Count} post(s) a concurrent cycle ingested first: {Titles}.",
                dropped.Count, string.Join(", ", dropped.Select(p => p.Title)));

            await db.SaveChangesAsync(cancellationToken);
            return dropped;
        }
    }

    /// <summary>
    /// Updates the denormalised post fields of tracked rows from the fresh feed data, recomputing
    /// the fingerprint when title/author changed (it derives from them). Metadata only —
    /// Status, AnnouncedUtc, FirstSeenUtc, and the attempt history are never touched.
    /// </summary>
    private async Task RefreshTrackedMetadataAsync(
        BlogAnnouncementsDbContext db,
        IReadOnlyCollection<(Guid PlatformPostId, AnnouncementCandidatePost Post)> trackedCandidates,
        CancellationToken cancellationToken)
    {
        if (trackedCandidates.Count == 0)
        {
            return;
        }

        var ids = trackedCandidates.Select(c => c.PlatformPostId).ToList();
        var rows = await db.AnnouncedBlogPosts
            .Where(p => ids.Contains(p.PlatformPostId))
            .ToDictionaryAsync(p => p.PlatformPostId, cancellationToken);

        foreach (var (platformPostId, post) in trackedCandidates)
        {
            if (!rows.TryGetValue(platformPostId, out var row))
            {
                continue;
            }

            var identityChanged = row.Title != post.Title || row.AuthorName != post.AuthorName;
            var changed = identityChanged;

            if (row.Title != post.Title) row.Title = post.Title;
            if (row.AuthorName != post.AuthorName) row.AuthorName = post.AuthorName;
            if (row.Url != post.Url) { row.Url = post.Url; changed = true; }
            if (row.AuthorAvatarUrl != post.AuthorAvatarUrl) { row.AuthorAvatarUrl = post.AuthorAvatarUrl; changed = true; }
            if (row.AuthorProfileUrl != post.AuthorProfileUrl) { row.AuthorProfileUrl = post.AuthorProfileUrl; changed = true; }
            if (row.Excerpt != post.Excerpt) { row.Excerpt = post.Excerpt; changed = true; }
            if (row.CoverImageUrl != post.CoverImageUrl) { row.CoverImageUrl = post.CoverImageUrl; changed = true; }

            if (identityChanged)
            {
                var fingerprint = AnnouncementFingerprint.Compute(post.AuthorName, post.Title, post.PublishedAt);
                if (row.Fingerprint != fingerprint)
                {
                    row.Fingerprint = fingerprint;
                }
            }

            if (changed)
            {
                _logger.LogDebug("Refreshed snapshotted metadata for tracked post '{Title}'.", post.Title);
            }
        }
    }

    /// <summary>
    /// Spacing between consecutive webhook sends: gives Discord clearly ordered message ids
    /// (near-simultaneous sends can render reordered after a client restart) and stays polite
    /// with rate limits. Skipped after dry-runs and after the last item.
    /// </summary>
    private static readonly TimeSpan InterMessageDelay = TimeSpan.FromSeconds(1);

    /// <summary>The states the automatic cycle may claim a row out of — its own queue.</summary>
    private static readonly AnnouncementStatus[] ClaimableStatuses =
        [AnnouncementStatus.Pending, AnnouncementStatus.Failed];

    /// <summary>
    /// Delivers Pending + Failed posts. Selection keeps the cap's guardrail semantics (the
    /// <em>newest</em> posts win a slot when over cap); delivery then runs strictly sequentially,
    /// oldest first with PlatformPostId as a tie-break (upstream publish times are often date-only
    /// midnights), so the channel reads chronologically and the order is deterministic.
    ///
    /// Every post is claimed in the database before it is sent and settled straight after, one row
    /// at a time — see <see cref="AnnouncementClaims"/>. That's what stops a concurrent cycle
    /// (second instance, overlapping schedule) announcing the same post twice, and it also means a
    /// process that dies mid-cycle can't lose the outcome of sends it already made.
    /// Returns (announced, failed).
    /// </summary>
    private async Task<(int Announced, int Failed)> DeliverQueueAsync(
        BlogAnnouncementsDbContext db,
        BlogAnnouncementsOptions options,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var cap = Math.Max(0, options.MaxAnnouncementsPerCycle);
        if (cap == 0)
        {
            return (0, 0);
        }

        // Claims abandoned by a crashed or recycled process would otherwise block their post forever.
        var released = await AnnouncementClaims.ReleaseStaleClaimsAsync(db, nowUtc, cancellationToken);
        if (released > 0)
        {
            _logger.LogWarning(
                "Reverted {Count} stale delivery claim(s) (older than {Timeout}) to Failed for retry.",
                released, AnnouncementClaims.StaleClaimTimeout);
        }

        // AsNoTracking: the rows are moved through Claimed and their outcome by AnnouncementClaims'
        // conditional updates, so nothing here may also be written via the change tracker.
        var selected = await db.AnnouncedBlogPosts
            .AsNoTracking()
            .Where(p => p.Status == AnnouncementStatus.Pending || p.Status == AnnouncementStatus.Failed)
            .OrderByDescending(p => p.PublishedAtUtc)
            .ThenBy(p => p.PlatformPostId)
            .Take(cap)
            .ToListAsync(cancellationToken);

        var queue = selected
            .OrderBy(p => p.PublishedAtUtc)
            .ThenBy(p => p.PlatformPostId)
            .ToList();

        var announcedCount = 0;
        var failedCount = 0;

        for (var i = 0; i < queue.Count; i++)
        {
            var post = queue[i];

            // Commit the claim before sending: a concurrent cycle that read the same row loses
            // this update and skips the post rather than announcing it a second time.
            var claimed = await AnnouncementClaims.TryClaimAsync(
                db, post.PlatformPostId, nowUtc, ClaimableStatuses, cancellationToken);
            if (!claimed)
            {
                _logger.LogInformation(
                    "Skipping '{Title}' — another delivery claimed it first (concurrent cycle).",
                    post.Title);
                continue;
            }

            var payload = AnnouncementPayloadFactory.FromPost(post);

            DeliveryResult result;
            try
            {
                result = await _announcer.AnnounceAsync(payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Delivery threw while announcing '{Title}'.", post.Title);
                result = DeliveryResult.Fail(null);
            }

            db.AnnouncementAttempts.Add(new AnnouncementAttempt
            {
                PlatformPostId = post.PlatformPostId,
                AttemptedUtc = nowUtc,
                HttpStatus = result.HttpStatus,
                Trigger = AnnouncementTrigger.Auto,
                Destination = "Discord",
                Outcome = result.DryRun ? "DryRun" : result.Success ? "Success" : "Failed",
            });
            await db.SaveChangesAsync(cancellationToken);

            // Dry-run posts nothing, so the claim is released back to the pre-claim state.
            var settledStatus = result.DryRun
                ? post.Status
                : result.Success
                    ? AnnouncementStatus.Announced
                    : AnnouncementStatus.Failed;
            var announcedUtc = !result.DryRun && result.Success ? nowUtc : post.AnnouncedUtc;

            await AnnouncementClaims.SettleAsync(
                db, post.PlatformPostId, settledStatus, announcedUtc, cancellationToken);

            if (result.DryRun)
            {
                // No message was sent, so no spacing is needed either.
                continue;
            }

            if (result.Success)
            {
                announcedCount++;
            }
            else
            {
                failedCount++;
            }

            // Space out real sends so Discord assigns clearly ordered message ids.
            if (i < queue.Count - 1)
            {
                await Task.Delay(InterMessageDelay, cancellationToken);
            }
        }

        return (announcedCount, failedCount);
    }
}

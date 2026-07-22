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

        var newCount = 0;
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
            db.AnnouncedBlogPosts.Add(new AnnouncedBlogPost
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
            });

            knownIds.Add(platformPostId);
            knownFingerprints.Add(fingerprint);
            newCount++;
            if (!withinWindow)
            {
                skippedCount++;
            }
        }

        await RefreshTrackedMetadataAsync(db, trackedCandidates, cancellationToken);

        // EF only writes modified columns; when nothing is new and nothing changed, this is a no-op.
        await db.SaveChangesAsync(cancellationToken);
        return (newCount, skippedCount);
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

    /// <summary>
    /// Delivers Pending + Failed posts. Selection keeps the cap's guardrail semantics (the
    /// <em>newest</em> posts win a slot when over cap); delivery then runs strictly sequentially,
    /// oldest first with PlatformPostId as a tie-break (upstream publish times are often date-only
    /// midnights), so the channel reads chronologically and the order is deterministic.
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

        var selected = await db.AnnouncedBlogPosts
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

            if (result.DryRun)
            {
                // Dry-run: leave the row Pending and post nothing (no spacing needed either).
                continue;
            }

            if (result.Success)
            {
                post.Status = AnnouncementStatus.Announced;
                post.AnnouncedUtc = nowUtc;
                announcedCount++;
            }
            else
            {
                post.Status = AnnouncementStatus.Failed;
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

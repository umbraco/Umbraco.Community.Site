using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.BlogAnnouncements.Api.Models;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using UmbracoCommunity.BlogAnnouncements.Infrastructure;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;

namespace UmbracoCommunity.BlogAnnouncements.Dashboard;

/// <summary>Filter/paging arguments for the Posts tab query.</summary>
public sealed record PostQuery(
    AnnouncementStatus? Status,
    string? Search,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Skip,
    int Take);

/// <summary>Outcome of a manual announce request.</summary>
public enum ManualAnnounceOutcome
{
    Delivered,
    PostNotFound,
    InFlight,
    InvalidTrigger,
    InvalidStatusForPostNow,
}

/// <summary>Outcome of a manual "mark as not announced" reset request.</summary>
public enum ResetOutcome
{
    Reset,
    PostNotFound,
    InFlight,
    InvalidStatus,
}

/// <summary>Result of a manual announce request — the outcome plus (when delivered) the delivery detail.</summary>
public sealed record ManualAnnounceResult(
    ManualAnnounceOutcome Outcome,
    DeliveryResult? Delivery = null,
    AnnouncementStatus? Status = null,
    DateTime? AnnouncedUtc = null);

/// <summary>
/// Read/query and manual-dispatch operations behind the Blog Announcements dashboard. Manual
/// repost / post-now share the automatic path's payload builder (<see cref="AnnouncementPayloadFactory"/>)
/// and delivery leg (<see cref="IDiscordAnnouncer"/>) so formatting never drifts, and are guarded
/// against concurrent in-flight delivery for the same post.
/// </summary>
public sealed class BlogAnnouncementDashboardService
{
    private const int DefaultWindowDays = 30;

    // In-flight guard across all requests — the service is a singleton.
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

    private readonly IDbContextFactory<BlogAnnouncementsDbContext> _contextFactory;
    private readonly IDiscordAnnouncer _announcer;
    private readonly IOptionsMonitor<BlogAnnouncementsOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<BlogAnnouncementDashboardService> _logger;

    public BlogAnnouncementDashboardService(
        IDbContextFactory<BlogAnnouncementsDbContext> contextFactory,
        IDiscordAnnouncer announcer,
        IOptionsMonitor<BlogAnnouncementsOptions> options,
        TimeProvider time,
        ILogger<BlogAnnouncementDashboardService> logger)
    {
        _contextFactory = contextFactory;
        _announcer = announcer;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<PostListResponse> ListPostsAsync(PostQuery query, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Default to the last 30 days when no explicit lower bound is supplied.
        var fromUtc = query.FromUtc ?? _time.GetUtcNow().UtcDateTime.AddDays(-DefaultWindowDays);

        var posts = db.AnnouncedBlogPosts.AsNoTracking()
            .Where(p => p.PublishedAtUtc >= fromUtc);

        if (query.ToUtc is { } toUtc)
        {
            posts = posts.Where(p => p.PublishedAtUtc <= toUtc);
        }

        if (query.Status is { } status)
        {
            posts = posts.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            posts = posts.Where(p =>
                EF.Functions.Like(p.Title, $"%{term}%") ||
                (p.AuthorName != null && EF.Functions.Like(p.AuthorName, $"%{term}%")));
        }

        var total = await posts.CountAsync(ct);

        var items = await posts
            .OrderByDescending(p => p.PublishedAtUtc)
            .ThenByDescending(p => p.FirstSeenUtc)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 200))
            .Select(p => new PostListItem
            {
                SphereId = p.SphereId,
                Title = p.Title,
                Url = p.Url,
                AuthorName = p.AuthorName,
                AuthorAvatarUrl = p.AuthorAvatarUrl,
                AuthorProfileUrl = p.AuthorProfileUrl,
                PublishedAtUtc = AsUtc(p.PublishedAtUtc),
                AnnouncedUtc = AsUtc(p.AnnouncedUtc),
                Status = (byte)p.Status,
                AttemptCount = p.Attempts.Count,
            })
            .ToListAsync(ct);

        return new PostListResponse { Total = total, Items = items };
    }

    public async Task<PostDetail?> GetPostAsync(Guid sphereId, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var post = await db.AnnouncedBlogPosts.AsNoTracking()
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(p => p.SphereId == sphereId, ct);

        if (post is null)
        {
            return null;
        }

        return new PostDetail
        {
            SphereId = post.SphereId,
            Title = post.Title,
            Url = post.Url,
            Excerpt = post.Excerpt,
            AuthorName = post.AuthorName,
            AuthorAvatarUrl = post.AuthorAvatarUrl,
            AuthorProfileUrl = post.AuthorProfileUrl,
            CoverImageUrl = post.CoverImageUrl,
            PublishedAtUtc = AsUtc(post.PublishedAtUtc),
            FirstSeenUtc = AsUtc(post.FirstSeenUtc),
            AnnouncedUtc = AsUtc(post.AnnouncedUtc),
            Fingerprint = post.Fingerprint,
            Status = (byte)post.Status,
            Attempts = post.Attempts
                .OrderByDescending(a => a.AttemptedUtc)
                .Select(a => new AttemptItem
                {
                    Id = a.Id,
                    AttemptedUtc = AsUtc(a.AttemptedUtc),
                    Outcome = a.Outcome,
                    HttpStatus = a.HttpStatus,
                    Trigger = (byte)a.Trigger,
                    Destination = a.Destination,
                })
                .ToList(),
        };
    }

    public async Task<RunListResponse> ListRunsAsync(int skip, int take, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var total = await db.AnnouncementRuns.CountAsync(ct);
        var items = await db.AnnouncementRuns.AsNoTracking()
            .OrderByDescending(r => r.RunUtc)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .Select(r => new RunListItem
            {
                Id = r.Id,
                RunUtc = AsUtc(r.RunUtc),
                Fetched = r.Fetched,
                New = r.New,
                Announced = r.Announced,
                Skipped = r.Skipped,
                Failed = r.Failed,
                DryRun = r.DryRun,
            })
            .ToListAsync(ct);

        return new RunListResponse { Total = total, Items = items };
    }

    public SettingsResponse GetSettings()
    {
        var options = _options.CurrentValue;
        return new SettingsResponse
        {
            RecencyWindowDays = options.RecencyWindowDays,
            MaxAnnouncementsPerCycle = options.MaxAnnouncementsPerCycle,
            DryRun = options.DryRun,
            WebhookConfigured = !string.IsNullOrWhiteSpace(options.Discord.WebhookUrl),
        };
    }

    /// <summary>
    /// Re-fires the delivery path for one post from the dashboard. Records an attempt with the
    /// supplied trigger and, on success, marks the post Announced. Post-now is only valid for posts
    /// that were never announced (Pending / SkippedTooOld).
    /// </summary>
    public async Task<ManualAnnounceResult> AnnounceAsync(Guid sphereId, AnnouncementTrigger trigger, CancellationToken ct)
    {
        if (trigger != AnnouncementTrigger.Repost && trigger != AnnouncementTrigger.PostNow)
        {
            return new ManualAnnounceResult(ManualAnnounceOutcome.InvalidTrigger);
        }

        // Guard against a second manual delivery for the same post while one is in flight.
        if (!_inFlight.TryAdd(sphereId, 0))
        {
            return new ManualAnnounceResult(ManualAnnounceOutcome.InFlight);
        }

        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            var post = await db.AnnouncedBlogPosts.FirstOrDefaultAsync(p => p.SphereId == sphereId, ct);
            if (post is null)
            {
                return new ManualAnnounceResult(ManualAnnounceOutcome.PostNotFound);
            }

            if (trigger == AnnouncementTrigger.PostNow &&
                post.Status is not (AnnouncementStatus.Pending or AnnouncementStatus.SkippedTooOld))
            {
                return new ManualAnnounceResult(ManualAnnounceOutcome.InvalidStatusForPostNow);
            }

            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var payload = AnnouncementPayloadFactory.FromPost(post);

            DeliveryResult result;
            try
            {
                result = await _announcer.AnnounceAsync(payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Manual delivery threw while announcing '{Title}'.", post.Title);
                result = DeliveryResult.Fail(null);
            }

            db.AnnouncementAttempts.Add(new AnnouncementAttempt
            {
                SphereId = post.SphereId,
                AttemptedUtc = nowUtc,
                HttpStatus = result.HttpStatus,
                Trigger = trigger,
                Destination = "Discord",
                Outcome = result.DryRun ? "DryRun" : result.Success ? "Success" : "Failed",
            });

            // Dry-run posts nothing and leaves the status untouched.
            if (!result.DryRun)
            {
                if (result.Success)
                {
                    post.Status = AnnouncementStatus.Announced;
                    post.AnnouncedUtc = nowUtc;
                }
                else
                {
                    post.Status = AnnouncementStatus.Failed;
                }
            }

            await db.SaveChangesAsync(ct);

            return new ManualAnnounceResult(
                ManualAnnounceOutcome.Delivered,
                result,
                post.Status,
                AsUtc(post.AnnouncedUtc));
        }
        finally
        {
            _inFlight.TryRemove(sphereId, out _);
        }
    }

    /// <summary>
    /// Marks an announced (or failed) post as not announced — a testing aid that lets the
    /// automatic cycle pick the post up again. Resets the row to Pending, clears AnnouncedUtc,
    /// and appends an attempt-history row (Outcome "Reset") so the action is auditable; the row
    /// and its delivery history are never deleted. Guarded by the same in-flight lock as manual
    /// delivery. Note: a reset post older than the recency window is re-evaluated by the next
    /// cycle under the normal rules and counts toward the per-cycle cap.
    /// </summary>
    public async Task<ResetOutcome> ResetAsync(Guid sphereId, CancellationToken ct)
    {
        if (!_inFlight.TryAdd(sphereId, 0))
        {
            return ResetOutcome.InFlight;
        }

        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);

            var post = await db.AnnouncedBlogPosts.FirstOrDefaultAsync(p => p.SphereId == sphereId, ct);
            if (post is null)
            {
                return ResetOutcome.PostNotFound;
            }

            if (post.Status is not (AnnouncementStatus.Announced or AnnouncementStatus.Failed))
            {
                return ResetOutcome.InvalidStatus;
            }

            post.Status = AnnouncementStatus.Pending;
            post.AnnouncedUtc = null;

            db.AnnouncementAttempts.Add(new AnnouncementAttempt
            {
                SphereId = post.SphereId,
                AttemptedUtc = _time.GetUtcNow().UtcDateTime,
                HttpStatus = null,
                Trigger = AnnouncementTrigger.Reset,
                Destination = "Discord",
                Outcome = "Reset",
            });

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Post '{Title}' was manually reset to not-announced.", post.Title);
            return ResetOutcome.Reset;
        }
        finally
        {
            _inFlight.TryRemove(sphereId, out _);
        }
    }

    /// <summary>
    /// Stamps a stored timestamp as UTC. The entities always hold UTC values, but EF materializes
    /// DateTimeKind.Unspecified (notably on SQLite), and System.Text.Json then serializes without
    /// the trailing "Z" — which browsers parse as local time. Applied at every DTO mapping site.
    /// </summary>
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) =>
        value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    /// <summary>Sends the canned test embed through the same delivery leg (honours dry-run).</summary>
    public async Task<DeliveryResult> SendTestMessageAsync(CancellationToken ct)
    {
        var payload = AnnouncementPayloadFactory.CreateTestMessage(_time.GetUtcNow());
        try
        {
            return await _announcer.AnnounceAsync(payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test message delivery threw.");
            return DeliveryResult.Fail(null);
        }
    }
}

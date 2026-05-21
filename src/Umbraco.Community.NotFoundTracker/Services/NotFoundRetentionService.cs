using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

/// <summary>
/// Purges old rows on a schedule. Three retention windows:
///   - Active hits older than <see cref="NotFoundTrackerOptions.ActiveRetentionDays"/>.
///   - Hits whose Status is Redirected/IgnoredManually older than ActionedRetentionDays.
///   - QueryString child rows older than QueryStringRetentionDays.
/// </summary>
public sealed class NotFoundRetentionService : BackgroundService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly IOptions<NotFoundTrackerOptions> _options;
    private readonly ILogger<NotFoundRetentionService> _logger;

    public NotFoundRetentionService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        IOptions<NotFoundTrackerOptions> options,
        ILogger<NotFoundRetentionService> logger)
    {
        _contextFactory = contextFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Value.RetentionSweepInterval);
        try
        {
            // Run once immediately on startup so a long-idle process doesn't hold stale rows for an hour.
            await SweepAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>Public for testing — runs the three deletes in their own short transactions.</summary>
    public async Task SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var opts = _options.Value;

        var activeCutoff = now.AddDays(-opts.ActiveRetentionDays);
        var actionedCutoff = now.AddDays(-opts.ActionedRetentionDays);
        var qsCutoff = now.AddDays(-opts.QueryStringRetentionDays);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var qsDeleted = await context.NotFoundHitQueryStrings
                .Where(q => q.LastSeenUtc < qsCutoff)
                .ExecuteDeleteAsync(ct);

            var actionedDeleted = await context.NotFoundHits
                .Where(h => (h.Status == HitStatus.Redirected || h.Status == HitStatus.IgnoredManually)
                            && h.LastSeenUtc < actionedCutoff)
                .ExecuteDeleteAsync(ct);

            var activeDeleted = await context.NotFoundHits
                .Where(h => h.Status == HitStatus.Active && h.LastSeenUtc < activeCutoff)
                .ExecuteDeleteAsync(ct);

            if (qsDeleted + actionedDeleted + activeDeleted > 0)
            {
                _logger.LogInformation(
                    "NotFoundTracker retention sweep: deleted {ActiveHits} active hits, {ActionedHits} actioned hits, {QueryStrings} query strings",
                    activeDeleted, actionedDeleted, qsDeleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotFoundTracker retention sweep failed");
        }
    }
}

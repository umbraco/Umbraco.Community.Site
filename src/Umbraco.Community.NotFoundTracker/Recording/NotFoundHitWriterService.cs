using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Recording;

/// <summary>
/// Drains the <see cref="NotFoundHitChannel"/> on a timer and batch-upserts hit rows
/// (+ their query-string children). Designed to do zero awaited I/O on the request hot path —
/// all DB writes happen here, off the request thread.
/// </summary>
public sealed class NotFoundHitWriterService : BackgroundService
{
    private readonly NotFoundHitChannel _channel;
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly IOptions<NotFoundTrackerOptions> _options;
    private readonly ILogger<NotFoundHitWriterService> _logger;

    public NotFoundHitWriterService(
        NotFoundHitChannel channel,
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        IOptions<NotFoundTrackerOptions> options,
        ILogger<NotFoundHitWriterService> logger)
    {
        _channel = channel;
        _contextFactory = contextFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var flushInterval = _options.Value.WriterFlushInterval;
        var batchSize = _options.Value.WriterBatchSize;
        using var timer = new PeriodicTimer(flushInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wake on either timer tick OR first item arriving in an empty channel,
                // whichever comes first. Gives low-latency drain after idle, batching under load.
                var timerTask = timer.WaitForNextTickAsync(stoppingToken).AsTask();
                var readyTask = _channel.Reader.WaitToReadAsync(stoppingToken).AsTask();
                await Task.WhenAny(timerTask, readyTask);

                await DrainOnceAsync(batchSize, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        // Graceful drain on shutdown: pull whatever remains in the channel.
        try
        {
            using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await DrainOnceAsync(int.MaxValue, drainCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error draining NotFoundTracker hit channel on shutdown");
        }
    }

    private async Task DrainOnceAsync(int maxEvents, CancellationToken ct)
    {
        var events = new List<NotFoundHitEvent>(Math.Min(maxEvents, 1024));
        while (events.Count < maxEvents && _channel.Reader.TryRead(out var evt))
        {
            events.Add(evt);
        }
        if (events.Count == 0) return;

        try
        {
            await UpsertBatchAsync(events, ct);
            _logger.LogDebug("NotFoundTracker drained {Count} events", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotFoundTracker batch upsert failed ({Count} events lost)", events.Count);
        }
    }

    private async Task UpsertBatchAsync(List<NotFoundHitEvent> events, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Group by (hostname, path) so each unique URL produces one DB statement at most.
        var groups = events.GroupBy(e => (e.Hostname, e.Path));

        foreach (var group in groups)
        {
            var (hostname, path) = group.Key;
            var groupList = group.ToList();
            var groupCount = groupList.Count;
            var maxOccurred = groupList.Max(e => e.OccurredUtc);
            var lastUA = groupList.OrderByDescending(e => e.OccurredUtc).First().UserAgent;

            var existing = await context.NotFoundHits
                .Include(h => h.QueryStrings)
                .FirstOrDefaultAsync(h => h.Hostname == hostname && h.Path == path, ct);

            NotFoundHitEntity hit;
            if (existing is null)
            {
                hit = new NotFoundHitEntity
                {
                    Hostname = hostname,
                    Path = path,
                    HitCount = groupCount,
                    FirstSeenUtc = groupList.Min(e => e.OccurredUtc),
                    LastSeenUtc = maxOccurred,
                    LastUserAgent = lastUA,
                    Status = HitStatus.Active,
                };
                context.NotFoundHits.Add(hit);
                await context.SaveChangesAsync(ct);
            }
            else
            {
                existing.HitCount += groupCount;
                existing.LastSeenUtc = maxOccurred > existing.LastSeenUtc ? maxOccurred : existing.LastSeenUtc;
                existing.LastUserAgent = lastUA ?? existing.LastUserAgent;
                hit = existing;
            }

            // Upsert query-string children. Skip events with no query string.
            var qsEvents = groupList.Where(e => !string.IsNullOrEmpty(e.QueryString)).ToList();
            if (qsEvents.Count == 0)
            {
                if (existing is not null) await context.SaveChangesAsync(ct);
                continue;
            }

            foreach (var qsGroup in qsEvents.GroupBy(e => e.QueryString!))
            {
                var qs = qsGroup.Key;
                var qsCount = qsGroup.Count();
                var qsLast = qsGroup.Max(e => e.OccurredUtc);

                var existingQs = hit.QueryStrings.FirstOrDefault(q => q.QueryString == qs);
                if (existingQs is null)
                {
                    hit.QueryStrings.Add(new NotFoundHitQueryStringEntity
                    {
                        QueryString = qs,
                        HitCount = qsCount,
                        LastSeenUtc = qsLast,
                    });
                }
                else
                {
                    existingQs.HitCount += qsCount;
                    existingQs.LastSeenUtc = qsLast > existingQs.LastSeenUtc ? qsLast : existingQs.LastSeenUtc;
                }
            }

            await context.SaveChangesAsync(ct);
        }
    }
}

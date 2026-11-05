using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.SearchTracker.Infrastructure;
using UmbracoCommunity.SearchTracker.Models.Entities;

namespace UmbracoCommunity.SearchTracker.Recording;

/// <summary>Drains <see cref="SearchEventChannel"/> on a timer and adds each batch to the daily term counts.</summary>
public sealed class SearchEventWriterService : BackgroundService
{
    private readonly SearchEventChannel _channel;
    private readonly IDbContextFactory<SearchTrackerDbContext> _contextFactory;
    private readonly SearchTrackerSchemaState _schemaState;
    private readonly IOptions<SearchTrackerOptions> _options;
    private readonly ILogger<SearchEventWriterService> _logger;

    public SearchEventWriterService(
        SearchEventChannel channel,
        IDbContextFactory<SearchTrackerDbContext> contextFactory,
        SearchTrackerSchemaState schemaState,
        IOptions<SearchTrackerOptions> options,
        ILogger<SearchEventWriterService> logger)
    {
        _channel = channel;
        _contextFactory = contextFactory;
        _schemaState = schemaState;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var flushInterval = _options.Value.WriterFlushInterval;
        var batchSize = _options.Value.WriterBatchSize;

        try
        {
            await _schemaState.Ready.WaitAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                waitCts.CancelAfter(flushInterval);
                try
                {
                    await _channel.Reader.WaitToReadAsync(waitCts.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                }

                await DrainOnceAsync(batchSize, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }

        if (!_schemaState.Ready.IsCompletedSuccessfully) return;

        try
        {
            using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await DrainOnceAsync(int.MaxValue, drainCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error draining SearchTracker channel on shutdown");
        }
    }

    internal async Task DrainOnceAsync(int maxEvents, CancellationToken ct)
    {
        var events = new List<SearchEvent>();
        while (events.Count < maxEvents && _channel.Reader.TryRead(out var evt))
        {
            events.Add(evt);
        }
        if (events.Count == 0) return;

        try
        {
            await UpsertBatchAsync(events, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchTracker batch write failed ({Count} searches lost)", events.Count);
        }
    }

    private async Task UpsertBatchAsync(List<SearchEvent> events, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var groups = events.GroupBy(e => (e.SiteKey, Day: DateOnly.FromDateTime(e.OccurredUtc), e.Term));
        foreach (var group in groups)
        {
            var (siteKey, day, term) = group.Key;
            var searches = group.Count();
            var zeroResults = group.Count(e => e.ResultCount == 0);

            var row = await context.SearchTermDaily
                .FirstOrDefaultAsync(r => r.SiteKey == siteKey && r.Day == day && r.Term == term, ct);

            if (row is null)
            {
                context.SearchTermDaily.Add(new SearchTermDailyEntity
                {
                    SiteKey = siteKey,
                    Day = day,
                    Term = term,
                    SearchCount = searches,
                    ZeroResultCount = zeroResults,
                });
            }
            else
            {
                row.SearchCount += searches;
                row.ZeroResultCount += zeroResults;
            }
        }

        await context.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.SearchTracker.Infrastructure;

namespace UmbracoCommunity.SearchTracker.Recording;

/// <summary>Deletes daily rows older than <see cref="SearchTrackerOptions.RetentionDays"/>.</summary>
public sealed class SearchTrackerRetentionService : BackgroundService
{
    private readonly IDbContextFactory<SearchTrackerDbContext> _contextFactory;
    private readonly SearchTrackerSchemaState _schemaState;
    private readonly IOptions<SearchTrackerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SearchTrackerRetentionService> _logger;

    public SearchTrackerRetentionService(
        IDbContextFactory<SearchTrackerDbContext> contextFactory,
        SearchTrackerSchemaState schemaState,
        IOptions<SearchTrackerOptions> options,
        TimeProvider timeProvider,
        ILogger<SearchTrackerRetentionService> logger)
    {
        _contextFactory = contextFactory;
        _schemaState = schemaState;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _schemaState.Ready.WaitAsync(stoppingToken);

            using var timer = new PeriodicTimer(_options.Value.RetentionSweepInterval);
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal async Task SweepAsync(CancellationToken ct)
    {
        var cutoff = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(-_options.Value.RetentionDays);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var deleted = await context.SearchTermDaily.Where(r => r.Day < cutoff).ExecuteDeleteAsync(ct);
            if (deleted > 0)
            {
                _logger.LogInformation("SearchTracker retention sweep deleted {Count} rows older than {Cutoff}", deleted, cutoff);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchTracker retention sweep failed");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Runs once on application startup AFTER migrations. Two responsibilities:
///   1. Insert any built-in <see cref="DefaultIgnoreRules"/> entries that aren't already
///      in the table, tagged <see cref="IgnoreRuleSource.AutoPreset"/>. Insert-if-missing —
///      editor deletions persist (never re-inserted on subsequent restarts).
///   2. (Task 9 of Plan 2 — added later) Reconcile <see cref="IgnoreRuleSource.ConfigSeeded"/>
///      entries against <see cref="NotFoundTrackerOptions.AdditionalAutoPresetRules"/>:
///      insert missing, delete orphans.
///
/// After both branches run, refreshes <see cref="IgnoreRuleMatcher"/> so the first request
/// sees the up-to-date rule set.
/// </summary>
public sealed class AutoPresetSeedingService : IHostedService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly INotFoundIgnoreRuleMatcher _matcher;
    private readonly IOptions<NotFoundTrackerOptions> _options;
    private readonly ILogger<AutoPresetSeedingService> _logger;

    public AutoPresetSeedingService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        INotFoundIgnoreRuleMatcher matcher,
        IOptions<NotFoundTrackerOptions> options,
        ILogger<AutoPresetSeedingService> logger)
    {
        _contextFactory = contextFactory;
        _matcher = matcher;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAutoPresetAsync(cancellationToken);
            await ReconcileConfigSeededAsync(cancellationToken);
            await _matcher.RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotFoundTracker auto-preset seeding failed");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Re-runs the seeding pass on demand (used by the management API). Equivalent to what
    /// StartAsync does on boot: inserts any missing auto-preset entries (respecting tombstones),
    /// reconciles config-seeded rows, refreshes the matcher.
    /// </summary>
    public async Task SeedAndReconcileAsync(CancellationToken ct)
    {
        await SeedAutoPresetAsync(ct);
        await ReconcileConfigSeededAsync(ct);
        await _matcher.RefreshAsync(ct);
    }

    private async Task SeedAutoPresetAsync(CancellationToken ct)
    {
        if (!_options.Value.SeedAutoPreset)
        {
            _logger.LogInformation("NotFoundTracker auto-preset seeding skipped (SeedAutoPreset=false)");
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Check seed history to respect editor deletions: once a preset rule has been
        // seeded, its record stays in NotFoundPresetSeedRecords even if the editor hard-deletes
        // the live NotFoundIgnoreRuleEntity row. We only insert rules that have never been seeded.
        var alreadySeeded = await context.NotFoundPresetSeedRecords
            .Select(r => new { r.Hostname, r.MatchType, r.Path })
            .ToListAsync(ct);

        var seededKeys = new HashSet<(string?, IgnoreMatchType, string)>(
            alreadySeeded.Select(r => (r.Hostname, r.MatchType, r.Path)));

        var inserted = 0;
        var now = DateTime.UtcNow;
        foreach (var rule in DefaultIgnoreRules.All)
        {
            var key = ((string?)null, rule.MatchType, rule.Path);
            if (seededKeys.Contains(key)) continue;

            context.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null,
                MatchType = rule.MatchType,
                Path = rule.Path,
                Source = IgnoreRuleSource.AutoPreset,
                CreatedUtc = now,
            });

            context.NotFoundPresetSeedRecords.Add(new NotFoundPresetSeedRecordEntity
            {
                Hostname = null,
                MatchType = rule.MatchType,
                Path = rule.Path,
                SeededUtc = now,
            });

            inserted++;
        }

        if (inserted > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("NotFoundTracker seeded {Count} auto-preset rules", inserted);
        }
    }

    private async Task ReconcileConfigSeededAsync(CancellationToken ct)
    {
        // Parse all config-declared rules up front so an invalid MatchType fails fast
        // before any DB writes happen.
        var desired = _options.Value.AdditionalAutoPresetRules
            .Select(AutoPresetRuleConfigParser.Parse)
            .Select(r => (r.Hostname, r.MatchType, r.Path))
            .ToHashSet();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existingConfigSeeded = await context.NotFoundIgnoreRules
            .Where(r => r.Source == IgnoreRuleSource.ConfigSeeded)
            .ToListAsync(ct);

        var existingKeys = existingConfigSeeded
            .Select(r => (r.Hostname, r.MatchType, r.Path))
            .ToHashSet();

        // Delete config-seeded rows no longer declared in config.
        var toDelete = existingConfigSeeded
            .Where(r => !desired.Contains((r.Hostname, r.MatchType, r.Path)))
            .ToList();
        if (toDelete.Count > 0)
        {
            context.NotFoundIgnoreRules.RemoveRange(toDelete);
        }

        // Insert config rules not yet present (with Source=ConfigSeeded only — a UserDefined
        // or AutoPreset row with the same path stays separate).
        var toInsert = desired
            .Where(key => !existingKeys.Contains(key))
            .ToList();
        foreach (var (hostname, matchType, path) in toInsert)
        {
            context.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = hostname,
                MatchType = matchType,
                Path = path,
                Source = IgnoreRuleSource.ConfigSeeded,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        if (toDelete.Count > 0 || toInsert.Count > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "NotFoundTracker reconciled config-seeded rules: +{Inserted} -{Deleted}",
                toInsert.Count, toDelete.Count);
        }
    }
}

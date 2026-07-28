using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// One-shot data fixup that normalizes existing hostnames in NotFoundHits, NotFoundIgnoreRules,
/// and NotFoundPresetSeedRecords using <see cref="UrlNormalizer.NormalizeHostname"/>. For hits,
/// duplicates that collapse to the same (Hostname, Path) are merged: HitCount summed, FirstSeen
/// kept as the earliest, LastSeen as the latest, and child QueryStrings reassigned to the
/// surviving row.
///
/// Idempotent: rows whose stored hostname already matches the normalized value are skipped, so
/// running this repeatedly is a no-op once the dataset is clean.
/// </summary>
public sealed class HostnameNormalizationService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly ILogger<HostnameNormalizationService> _logger;

    public HostnameNormalizationService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        ILogger<HostnameNormalizationService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task NormalizeAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var hitsChanged = await NormalizeHitsAsync(context, ct);
        var rulesChanged = await NormalizeSimpleHostnamesAsync<NotFoundIgnoreRuleEntity>(
            context, context.NotFoundIgnoreRules, e => e.Hostname, (e, v) => e.Hostname = v, ct);
        var presetsChanged = await NormalizeSimpleHostnamesAsync<NotFoundPresetSeedRecordEntity>(
            context, context.NotFoundPresetSeedRecords, e => e.Hostname, (e, v) => e.Hostname = v, ct);

        if (hitsChanged + rulesChanged + presetsChanged > 0)
        {
            _logger.LogInformation(
                "NotFoundTracker hostname normalization: {Hits} hits, {Rules} ignore rules, {Presets} preset records updated",
                hitsChanged, rulesChanged, presetsChanged);
        }
    }

    private static async Task<int> NormalizeHitsAsync(NotFoundTrackerDbContext context, CancellationToken ct)
    {
        // Pull every row up-front: dataset is small (admin-only 404 log) and we need to detect
        // cross-row duplicates that only emerge after normalizing the hostname.
        var hits = await context.NotFoundHits.Include(h => h.QueryStrings).ToListAsync(ct);
        if (hits.Count == 0) return 0;

        var changed = 0;
        var groups = hits
            .Select(h => (Hit: h, Normalized: UrlNormalizer.NormalizeHostname(h.Hostname)))
            .GroupBy(x => (Hostname: x.Normalized, x.Hit.Path));

        foreach (var group in groups)
        {
            var members = group.OrderBy(x => x.Hit.Id).ToList();
            var survivor = members[0].Hit;
            var normalizedHost = group.Key.Hostname;

            if (members.Count == 1)
            {
                if (survivor.Hostname != normalizedHost)
                {
                    survivor.Hostname = normalizedHost;
                    changed++;
                }
                continue;
            }

            // Merge: HitCount sum, FirstSeen min, LastSeen max, keep most-recent UserAgent.
            survivor.Hostname = normalizedHost;
            for (var i = 1; i < members.Count; i++)
            {
                var dup = members[i].Hit;
                survivor.HitCount += dup.HitCount;
                if (dup.FirstSeenUtc < survivor.FirstSeenUtc) survivor.FirstSeenUtc = dup.FirstSeenUtc;
                if (dup.LastSeenUtc > survivor.LastSeenUtc)
                {
                    survivor.LastSeenUtc = dup.LastSeenUtc;
                    survivor.LastUserAgent = dup.LastUserAgent ?? survivor.LastUserAgent;
                }

                // Reassign query strings to the survivor; the child rows already have HitId, so
                // updating the FK is enough — EF will UPDATE rather than re-insert.
                foreach (var qs in dup.QueryStrings)
                {
                    qs.HitId = survivor.Id;
                }

                context.NotFoundHits.Remove(dup);
            }
            changed += members.Count;
        }

        if (changed > 0)
        {
            await context.SaveChangesAsync(ct);
        }
        return changed;
    }

    private static async Task<int> NormalizeSimpleHostnamesAsync<T>(
        NotFoundTrackerDbContext context,
        DbSet<T> set,
        Func<T, string?> getHostname,
        Action<T, string?> setHostname,
        CancellationToken ct) where T : class
    {
        var rows = await set.ToListAsync(ct);
        var changed = 0;
        foreach (var row in rows)
        {
            var current = getHostname(row);
            if (string.IsNullOrEmpty(current)) continue;
            var normalized = UrlNormalizer.NormalizeHostname(current);
            if (normalized != current)
            {
                setHostname(row, normalized);
                changed++;
            }
        }

        if (changed > 0)
        {
            await context.SaveChangesAsync(ct);
        }
        return changed;
    }
}

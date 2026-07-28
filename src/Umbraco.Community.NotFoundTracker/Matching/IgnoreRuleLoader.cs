using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Matching;

/// <summary>
/// Reads all <see cref="NotFoundIgnoreRuleEntity"/> rows and projects them into an
/// immutable <see cref="IgnoreRuleSnapshot"/>. Called by <see cref="IgnoreRuleMatcher"/>
/// on every <c>RefreshAsync</c>.
/// </summary>
public sealed class IgnoreRuleLoader
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;

    public IgnoreRuleLoader(IDbContextFactory<NotFoundTrackerDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IgnoreRuleSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rules = await context.NotFoundIgnoreRules
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var global = new HostBucket();
        var byHost = new Dictionary<string, HostBucket>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            var bucket = ResolveBucket(rule, global, byHost);
            AddToBucket(bucket, rule);
        }

        return new IgnoreRuleSnapshot(global, byHost);
    }

    private static HostBucket ResolveBucket(
        NotFoundIgnoreRuleEntity rule,
        HostBucket global,
        Dictionary<string, HostBucket> byHost)
    {
        if (string.IsNullOrEmpty(rule.Hostname))
        {
            return global;
        }

        // Hostnames are stored as the editor entered them; lookup is case-insensitive,
        // so lowercase the key here. The matcher lowercases the request hostname too
        // (via UrlNormalizer), so the keys agree.
        var key = rule.Hostname.ToLowerInvariant();
        if (!byHost.TryGetValue(key, out var bucket))
        {
            bucket = new HostBucket();
            byHost[key] = bucket;
        }
        return bucket;
    }

    private static void AddToBucket(HostBucket bucket, NotFoundIgnoreRuleEntity rule)
    {
        // Paths in storage are already lowercased + normalized — but defensively
        // lowercase here so any test data with mixed casing still maps consistently.
        var path = rule.Path.ToLowerInvariant();
        switch (rule.MatchType)
        {
            case IgnoreMatchType.Exact:
                bucket.ExactPaths.Add(path);
                break;
            case IgnoreMatchType.PathPrefix:
                bucket.PrefixPaths.Add(path);
                break;
        }
    }
}

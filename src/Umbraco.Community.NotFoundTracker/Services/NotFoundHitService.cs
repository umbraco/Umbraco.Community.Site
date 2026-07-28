using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public sealed class NotFoundHitService : INotFoundHitService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;

    public NotFoundHitService(IDbContextFactory<NotFoundTrackerDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<(IReadOnlyList<HitListRow> items, int total)> ListAsync(
        HitListQuery query, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var q = context.NotFoundHits.AsNoTracking().AsQueryable();

        if (!scope.HasFullAccess)
        {
            if (scope.AccessibleHostnames.Count == 0)
            {
                return (Array.Empty<HitListRow>(), 0);
            }
            q = q.Where(h => scope.AccessibleHostnames.Contains(h.Hostname));
        }

        if (query.Hostnames is { Count: > 0 })
        {
            var allowed = query.Hostnames.Where(scope.CanAccessHostname).ToList();
            if (allowed.Count == 0)
            {
                return (Array.Empty<HitListRow>(), 0);
            }
            q = q.Where(h => allowed.Contains(h.Hostname));
        }

        if (query.Status.HasValue)
        {
            q = q.Where(h => h.Status == query.Status.Value);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            q = q.Where(h => h.Path.Contains(search));
        }

        q = query.Sort switch
        {
            HitSort.Popularity => q.OrderByDescending(h => h.HitCount).ThenByDescending(h => h.LastSeenUtc),
            HitSort.FirstSeen => q.OrderByDescending(h => h.FirstSeenUtc),
            HitSort.QueryStringCount => q.OrderByDescending(h => h.QueryStrings.Count()).ThenByDescending(h => h.LastSeenUtc),
            _ => q.OrderByDescending(h => h.LastSeenUtc),
        };

        var total = await q.CountAsync(ct);
        var items = await q
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(h => new HitListRow(h, h.QueryStrings.Count()))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<NotFoundHitEntity?> GetAsync(int id, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hit = await context.NotFoundHits
            .AsNoTracking()
            .Include(h => h.QueryStrings)
            .FirstOrDefaultAsync(h => h.Id == id, ct);

        if (hit is null) return null;
        return scope.CanAccessHostname(hit.Hostname) ? hit : null;
    }

    public async Task<IReadOnlyList<string>> GetDistinctHostnamesAsync(UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hostnames = await context.NotFoundHits
            .AsNoTracking()
            .Select(h => h.Hostname)
            .Distinct()
            .ToListAsync(ct);

        if (scope.HasFullAccess) return hostnames;
        return hostnames.Where(h => scope.AccessibleHostnames.Contains(h)).ToList();
    }

    public async Task<bool> DeleteAsync(int id, UserScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hit = await context.NotFoundHits.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (hit is null) return false;
        if (!scope.CanAccessHostname(hit.Hostname)) return false;

        context.NotFoundHits.Remove(hit);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(int processed, int skipped)> BulkDeleteAsync(
        IEnumerable<int> ids, UserScope scope, CancellationToken ct)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return (0, 0);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var hits = await context.NotFoundHits.Where(h => idList.Contains(h.Id)).ToListAsync(ct);

        var processable = hits.Where(h => scope.CanAccessHostname(h.Hostname)).ToList();
        var skipped = idList.Count - processable.Count;

        context.NotFoundHits.RemoveRange(processable);
        await context.SaveChangesAsync(ct);

        return (processable.Count, skipped);
    }
}

using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.SearchTracker.Infrastructure;
using UmbracoCommunity.SearchTracker.Models.Entities;

namespace UmbracoCommunity.SearchTracker.Reporting;

public enum TermSort : byte
{
    Searches = 0,
    FewestResults = 1,
    LastSearched = 2,
    Term = 3,
}

public sealed record ReportFilter(Guid? SiteKey, DateOnly From, DateOnly To);

public sealed record DailyCount(DateOnly Day, int Searches, int ZeroResultSearches);

public sealed record SearchSummary(int TotalSearches, int ZeroResultSearches, int UniqueTerms, IReadOnlyList<DailyCount> Days);

public sealed record TermStat(string Term, int Searches, int? LatestResultCount, DateOnly LastSearched);

public sealed record TermQuery(string? Contains, bool NothingFoundOnly, TermSort Sort, int Skip, int Take);

public interface ISearchReportService
{
    Task<IReadOnlyList<Guid>> GetSiteKeysAsync(SiteScope scope, CancellationToken ct);
    Task<SearchSummary> GetSummaryAsync(ReportFilter filter, SiteScope scope, CancellationToken ct);
    Task<(IReadOnlyList<TermStat> Items, int Total)> GetTermsAsync(ReportFilter filter, TermQuery query, SiteScope scope, CancellationToken ct);
    Task<IReadOnlyList<DailyCount>> GetTermTrendAsync(ReportFilter filter, string term, SiteScope scope, CancellationToken ct);
    Task<int> DeleteTermAsync(Guid? siteKey, string term, SiteScope scope, CancellationToken ct);
}

internal sealed class SearchReportService : ISearchReportService
{
    private readonly IDbContextFactory<SearchTrackerDbContext> _contextFactory;

    public SearchReportService(IDbContextFactory<SearchTrackerDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<Guid>> GetSiteKeysAsync(SiteScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var keys = await ApplyScope(context.SearchTermDaily, null, scope)
            .Select(r => r.SiteKey)
            .Distinct()
            .ToListAsync(ct);
        return keys;
    }

    public async Task<SearchSummary> GetSummaryAsync(ReportFilter filter, SiteScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rows = Filtered(context, filter, scope);

        var byDay = await rows
            .GroupBy(r => r.Day)
            .Select(g => new { Day = g.Key, Searches = g.Sum(r => r.SearchCount), Zero = g.Sum(r => r.ZeroResultCount) })
            .ToListAsync(ct);
        var uniqueTerms = await rows.Select(r => r.Term).Distinct().CountAsync(ct);

        var lookup = byDay.ToDictionary(d => d.Day);
        var days = new List<DailyCount>();
        for (var day = filter.From; day <= filter.To; day = day.AddDays(1))
        {
            days.Add(lookup.TryGetValue(day, out var d)
                ? new DailyCount(day, d.Searches, d.Zero)
                : new DailyCount(day, 0, 0));
        }

        return new SearchSummary(byDay.Sum(d => d.Searches), byDay.Sum(d => d.Zero), uniqueTerms, days);
    }

    public async Task<(IReadOnlyList<TermStat> Items, int Total)> GetTermsAsync(
        ReportFilter filter, TermQuery query, SiteScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var grouped = BuildTermsQuery(context, filter, query, scope);

        var total = await grouped.CountAsync(ct);

        var items = await OrderTerms(grouped, query.Sort)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(ct);

        return (items.Select(g => new TermStat(g.Term, g.Searches, g.Latest, g.Last)).ToList(), total);
    }

    internal static IQueryable<TermRow> BuildTermsQuery(SearchTrackerDbContext context, ReportFilter filter, TermQuery query, SiteScope scope)
    {
        var rows = Filtered(context, filter, scope);

        var contains = query.Contains?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(contains))
        {
            rows = rows.Where(r => r.Term.Contains(contains));
        }

        var grouped = rows
            .GroupBy(r => r.Term)
            .Select(g => new TermRow
            {
                Term = g.Key,
                Searches = g.Sum(r => r.SearchCount),
                Latest = g.OrderByDescending(r => r.Day).Select(r => r.LastResultCount).FirstOrDefault(),
                Last = g.Max(r => r.Day),
            });

        return query.NothingFoundOnly ? grouped.Where(g => g.Latest == 0) : grouped;
    }

    internal static IQueryable<TermRow> OrderTerms(IQueryable<TermRow> terms, TermSort sort) => sort switch
    {
        TermSort.FewestResults => terms.OrderBy(g => g.Latest == null).ThenBy(g => g.Latest).ThenByDescending(g => g.Searches).ThenBy(g => g.Term),
        TermSort.LastSearched => terms.OrderByDescending(g => g.Last).ThenByDescending(g => g.Searches).ThenBy(g => g.Term),
        TermSort.Term => terms.OrderBy(g => g.Term),
        _ => terms.OrderByDescending(g => g.Searches).ThenBy(g => g.Term),
    };

    internal sealed class TermRow
    {
        public string Term { get; init; } = string.Empty;
        public int Searches { get; init; }
        public int? Latest { get; init; }
        public DateOnly Last { get; init; }
    }

    public async Task<IReadOnlyList<DailyCount>> GetTermTrendAsync(ReportFilter filter, string term, SiteScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var byDay = await Filtered(context, filter, scope)
            .Where(r => r.Term == term)
            .GroupBy(r => r.Day)
            .Select(g => new { Day = g.Key, Searches = g.Sum(r => r.SearchCount), Zero = g.Sum(r => r.ZeroResultCount) })
            .ToDictionaryAsync(d => d.Day, ct);

        var days = new List<DailyCount>();
        for (var day = filter.From; day <= filter.To; day = day.AddDays(1))
        {
            days.Add(byDay.TryGetValue(day, out var d) ? new DailyCount(day, d.Searches, d.Zero) : new DailyCount(day, 0, 0));
        }
        return days;
    }

    public async Task<int> DeleteTermAsync(Guid? siteKey, string term, SiteScope scope, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await ApplyScope(context.SearchTermDaily, siteKey, scope)
            .Where(r => r.Term == term)
            .ExecuteDeleteAsync(ct);
    }

    private static IQueryable<SearchTermDailyEntity> Filtered(SearchTrackerDbContext context, ReportFilter filter, SiteScope scope)
        => ApplyScope(context.SearchTermDaily, filter.SiteKey, scope)
            .Where(r => r.Day >= filter.From && r.Day <= filter.To);

    private static IQueryable<SearchTermDailyEntity> ApplyScope(IQueryable<SearchTermDailyEntity> rows, Guid? siteKey, SiteScope scope)
    {
        if (siteKey.HasValue)
        {
            return scope.CanAccess(siteKey.Value)
                ? rows.Where(r => r.SiteKey == siteKey.Value)
                : rows.Where(_ => false);
        }

        if (scope.HasFullAccess) return rows;

        var keys = scope.SiteKeys.ToList();
        return rows.Where(r => keys.Contains(r.SiteKey));
    }
}

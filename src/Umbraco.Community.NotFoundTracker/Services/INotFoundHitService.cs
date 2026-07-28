using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Services;

public interface INotFoundHitService
{
    Task<(IReadOnlyList<HitListRow> items, int total)> ListAsync(HitListQuery query, UserScope scope, CancellationToken ct);
    Task<NotFoundHitEntity?> GetAsync(int id, UserScope scope, CancellationToken ct);
    Task<IReadOnlyList<string>> GetDistinctHostnamesAsync(UserScope scope, CancellationToken ct);
    Task<bool> DeleteAsync(int id, UserScope scope, CancellationToken ct);
    Task<(int processed, int skipped)> BulkDeleteAsync(IEnumerable<int> ids, UserScope scope, CancellationToken ct);
}

public sealed class HitListQuery
{
    /// <summary>
    /// Empty/null = no hostname filter. Multiple values match any of the listed hostnames
    /// (used by the grouped-hostname dropdown which can submit several aliases for one node).
    /// </summary>
    public IReadOnlyList<string>? Hostnames { get; init; }
    public HitStatus? Status { get; init; } = HitStatus.Active;
    public string? Search { get; init; }
    public HitSort Sort { get; init; } = HitSort.RecentlySeen;
    public int Skip { get; init; }
    public int Take { get; init; } = 25;
}

public sealed record HitListRow(NotFoundHitEntity Hit, int QueryStringCount);

public enum HitSort
{
    RecentlySeen = 0,
    Popularity = 1,
    FirstSeen = 2,
    QueryStringCount = 3,
}

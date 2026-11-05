using UmbracoCommunity.SearchTracker.Reporting;

namespace UmbracoCommunity.SearchTracker.Api.Models;

public sealed record SiteItem(Guid Key, string Name);

public sealed record SummaryResponse(
    DateOnly From,
    DateOnly To,
    int TotalSearches,
    int ZeroResultSearches,
    int UniqueTerms,
    IReadOnlyList<DailyCount> Days);

public sealed record TermListResponse(int Total, IReadOnlyList<TermStat> Items);

public sealed record DeleteTermResponse(int Deleted);

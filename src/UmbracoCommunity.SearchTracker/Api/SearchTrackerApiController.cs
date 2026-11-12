using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.SearchTracker.Api.Models;
using UmbracoCommunity.SearchTracker.Reporting;

namespace UmbracoCommunity.SearchTracker.Api;

public sealed class SearchTrackerApiController : SearchTrackerApiControllerBase
{
    private const int DefaultRangeDays = 30;
    private const int MaxRangeDays = 366;

    private readonly ISearchReportService _reports;
    private readonly ISiteScopeService _scope;
    private readonly IEntityService _entityService;
    private readonly ISearchPageUrlProvider _searchPageUrls;
    private readonly TimeProvider _timeProvider;

    public SearchTrackerApiController(
        ISearchReportService reports,
        ISiteScopeService scope,
        IEntityService entityService,
        ISearchPageUrlProvider searchPageUrls,
        TimeProvider timeProvider)
    {
        _reports = reports;
        _scope = scope;
        _entityService = entityService;
        _searchPageUrls = searchPageUrls;
        _timeProvider = timeProvider;
    }

    [HttpGet("sites")]
    public async Task<ActionResult<IReadOnlyList<SiteItem>>> GetSites(CancellationToken ct)
    {
        var keys = await _reports.GetSiteKeysAsync(_scope.GetCurrentScope(), ct);
        if (keys.Count == 0) return Ok(Array.Empty<SiteItem>());

        var names = _entityService.GetAll(UmbracoObjectTypes.Document, keys.ToArray())
            .ToDictionary(e => e.Key, e => e.Name ?? string.Empty);

        var sites = keys
            .Select(k => names.TryGetValue(k, out var name)
                ? new SiteItem(k, name, _searchPageUrls.GetSearchPageUrl(k))
                : new SiteItem(k, "(deleted site)", null))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Ok(sites);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryResponse>> GetSummary(
        [FromQuery] Guid? siteKey, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var filter = BuildFilter(siteKey, from, to);
        var summary = await _reports.GetSummaryAsync(filter, _scope.GetCurrentScope(), ct);
        return Ok(new SummaryResponse(filter.From, filter.To, summary.TotalSearches, summary.ZeroResultSearches, summary.UniqueTerms, summary.Days));
    }

    [HttpGet("terms")]
    public async Task<ActionResult<TermListResponse>> GetTerms(
        [FromQuery] Guid? siteKey,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? contains,
        [FromQuery] bool nothingFoundOnly = false,
        [FromQuery] byte sort = 0,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        var query = new TermQuery(
            contains,
            nothingFoundOnly,
            Enum.IsDefined(typeof(TermSort), sort) ? (TermSort)sort : TermSort.Searches,
            Math.Max(0, skip),
            Math.Clamp(take, 1, 200));

        var (items, total) = await _reports.GetTermsAsync(BuildFilter(siteKey, from, to), query, _scope.GetCurrentScope(), ct);
        return Ok(new TermListResponse(total, items));
    }

    [HttpGet("terms/trend")]
    public async Task<ActionResult<IReadOnlyList<DailyCount>>> GetTermTrend(
        [FromQuery] string term, [FromQuery] Guid? siteKey, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(term)) return BadRequest();
        return Ok(await _reports.GetTermTrendAsync(BuildFilter(siteKey, from, to), term, _scope.GetCurrentScope(), ct));
    }

    [HttpDelete("terms")]
    public async Task<ActionResult<DeleteTermResponse>> DeleteTerm([FromQuery] string term, [FromQuery] Guid? siteKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(term)) return BadRequest();
        var deleted = await _reports.DeleteTermAsync(siteKey, term, _scope.GetCurrentScope(), ct);
        return Ok(new DeleteTermResponse(deleted));
    }

    private ReportFilter BuildFilter(Guid? siteKey, DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var end = to is { } t && t <= today ? t : today;
        var start = from ?? end.AddDays(-(DefaultRangeDays - 1));
        if (start > end) start = end;
        if (end.DayNumber - start.DayNumber >= MaxRangeDays) start = end.AddDays(-(MaxRangeDays - 1));
        return new ReportFilter(siteKey, start, end);
    }
}

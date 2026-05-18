using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.NotFoundTracker.Models.Api;
using Umbraco.Community.NotFoundTracker.Models.Entities;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Controllers;

public sealed class NotFoundTrackerApiController : NotFoundTrackerApiControllerBase
{
    private readonly INotFoundHitService _hits;
    private readonly INotFoundIgnoreRuleService _rules;
    private readonly INotFoundRedirectService _redirects;
    private readonly INotFoundUserScopeService _scope;

    public NotFoundTrackerApiController(
        INotFoundHitService hits,
        INotFoundIgnoreRuleService rules,
        INotFoundRedirectService redirects,
        INotFoundUserScopeService scope)
    {
        _hits = hits;
        _rules = rules;
        _redirects = redirects;
        _scope = scope;
    }

    [HttpGet("hits")]
    public async Task<ActionResult<HitListResponse>> ListHits(
        [FromQuery] string? hostname,
        [FromQuery] byte? status,
        [FromQuery] string? search,
        [FromQuery] byte sort = 0,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        var query = new HitListQuery
        {
            Hostname = string.IsNullOrEmpty(hostname) ? null : hostname.ToLowerInvariant(),
            Status = status.HasValue ? (HitStatus)status.Value : HitStatus.Active,
            Search = search,
            Sort = (HitSort)sort,
            Skip = Math.Max(0, skip),
            Take = Math.Clamp(take, 1, 200),
        };

        var (items, total) = await _hits.ListAsync(query, _scope.GetCurrentScope(), ct);
        return Ok(new HitListResponse
        {
            Total = total,
            Items = items.Select(MapItem).ToList(),
        });
    }

    [HttpGet("hits/{id:int}")]
    public async Task<ActionResult<HitDetail>> GetHit(int id, CancellationToken ct)
    {
        var hit = await _hits.GetAsync(id, _scope.GetCurrentScope(), ct);
        if (hit is null) return NotFound();

        return Ok(new HitDetail
        {
            Id = hit.Id,
            Hostname = hit.Hostname,
            Path = hit.Path,
            HitCount = hit.HitCount,
            FirstSeenUtc = hit.FirstSeenUtc,
            LastSeenUtc = hit.LastSeenUtc,
            LastUserAgent = hit.LastUserAgent,
            Status = (byte)hit.Status,
            QueryStrings = hit.QueryStrings.Select(q => new HitQueryStringItem
            {
                QueryString = q.QueryString,
                HitCount = q.HitCount,
                LastSeenUtc = q.LastSeenUtc,
            }).ToList(),
        });
    }

    [HttpGet("hits/hostnames")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetHostnames(CancellationToken ct)
    {
        var hosts = await _hits.GetDistinctHostnamesAsync(_scope.GetCurrentScope(), ct);
        return Ok(hosts);
    }

    private static HitListItem MapItem(NotFoundHitEntity h) => new()
    {
        Id = h.Id,
        Hostname = h.Hostname,
        Path = h.Path,
        HitCount = h.HitCount,
        FirstSeenUtc = h.FirstSeenUtc,
        LastSeenUtc = h.LastSeenUtc,
        Status = (byte)h.Status,
    };
}

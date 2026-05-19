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
    private readonly Infrastructure.AutoPresetSeedingService _seedingService;

    public NotFoundTrackerApiController(
        INotFoundHitService hits,
        INotFoundIgnoreRuleService rules,
        INotFoundRedirectService redirects,
        INotFoundUserScopeService scope,
        Infrastructure.AutoPresetSeedingService seedingService)
    {
        _hits = hits;
        _rules = rules;
        _redirects = redirects;
        _scope = scope;
        _seedingService = seedingService;
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

        var (items, total) = await _hits.ListAsync(query, await _scope.GetCurrentScopeAsync(ct), ct);
        return Ok(new HitListResponse
        {
            Total = total,
            Items = items.Select(MapItem).ToList(),
        });
    }

    [HttpGet("hits/{id:int}")]
    public async Task<ActionResult<HitDetail>> GetHit(int id, CancellationToken ct)
    {
        var hit = await _hits.GetAsync(id, await _scope.GetCurrentScopeAsync(ct), ct);
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
        var hosts = await _hits.GetDistinctHostnamesAsync(await _scope.GetCurrentScopeAsync(ct), ct);
        return Ok(hosts);
    }

    [HttpDelete("hits/{id:int}")]
    public async Task<IActionResult> DeleteHit(int id, CancellationToken ct)
    {
        var ok = await _hits.DeleteAsync(id, await _scope.GetCurrentScopeAsync(ct), ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("hits/bulk-delete")]
    public async Task<ActionResult<BulkOpResponse>> BulkDeleteHits([FromBody] BulkIdsRequest request, CancellationToken ct)
    {
        var (processed, skipped) = await _hits.BulkDeleteAsync(request.Ids, await _scope.GetCurrentScopeAsync(ct), ct);
        return Ok(new BulkOpResponse { Processed = processed, Skipped = skipped });
    }

    [HttpPost("hits/{id:int}/redirect")]
    public async Task<IActionResult> CreateRedirect(int id, [FromBody] CreateRedirectRequest request, CancellationToken ct)
    {
        var result = await _redirects.CreateRedirectForHitAsync(id, request.TargetContentKey, request.Culture, await _scope.GetCurrentScopeAsync(ct), ct);

        return result.Kind switch
        {
            RedirectResultKind.Ok => NoContent(),
            RedirectResultKind.HitNotFound => NotFound(),
            RedirectResultKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { reason = result.Reason }),
            RedirectResultKind.TargetContentNotFound => BadRequest(new { reason = "Target content not found." }),
            RedirectResultKind.TargetContentNotAccessible => StatusCode(StatusCodes.Status403Forbidden, new { reason = result.Reason }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { reason = result.Reason ?? "Redirect failed." }),
        };
    }

    [HttpPost("hits/{id:int}/ignore")]
    public async Task<IActionResult> CreateIgnoreFromHit(
        int id, [FromBody] CreateIgnoreRuleRequest request, CancellationToken ct)
    {
        var scope = await _scope.GetCurrentScopeAsync(ct);

        // Resolve the hit so we can flip its status after rule creation.
        var hit = await _hits.GetAsync(id, scope, ct);
        if (hit is null) return NotFound();

        // Create the rule.
        var ruleResult = await _rules.CreateAsync(
            new CreateIgnoreRuleInput(request.Path, (IgnoreMatchType)request.MatchType, request.Hostname, request.Note),
            scope, ct);

        if (ruleResult.Result == IgnoreRuleMutationResult.Forbidden)
            return StatusCode(StatusCodes.Status403Forbidden, new { reason = ruleResult.Reason });
        if (ruleResult.Result == IgnoreRuleMutationResult.Conflict)
            return Conflict(new { reason = ruleResult.Reason });
        if (ruleResult.Result == IgnoreRuleMutationResult.InvalidInput)
            return BadRequest(new { reason = ruleResult.Reason });

        // Delete the hit — the editor's intent ("ignore this URL") means stop showing it.
        // The ignore rule prevents future recording; deleting the existing row clears it now.
        await _hits.DeleteAsync(id, scope, ct);

        return NoContent();
    }

    [HttpPost("hits/bulk-ignore")]
    public async Task<ActionResult<BulkOpResponse>> BulkIgnoreHits([FromBody] BulkIgnoreRequest request, CancellationToken ct)
    {
        var scope = await _scope.GetCurrentScopeAsync(ct);
        var matchType = (IgnoreMatchType)request.MatchType;
        var processed = 0;
        var skipped = 0;

        foreach (var id in request.Ids)
        {
            var hit = await _hits.GetAsync(id, scope, ct);
            if (hit is null) { skipped++; continue; }

            var ruleResult = await _rules.CreateAsync(
                new CreateIgnoreRuleInput(hit.Path, matchType, hit.Hostname, Note: null),
                scope, ct);

            if (ruleResult.Result == IgnoreRuleMutationResult.Ok)
            {
                await _hits.DeleteAsync(id, scope, ct);
                processed++;
            }
            else
            {
                // Duplicate rule, forbidden, etc. — count as skipped.
                skipped++;
            }
        }

        return Ok(new BulkOpResponse { Processed = processed, Skipped = skipped });
    }

    [HttpGet("ignore-rules")]
    public async Task<ActionResult<IReadOnlyList<IgnoreRuleItem>>> ListIgnoreRules(CancellationToken ct)
    {
        var rules = await _rules.ListAsync(await _scope.GetCurrentScopeAsync(ct), ct);
        return Ok(rules.Select(MapRule).ToList());
    }

    [HttpPost("ignore-rules")]
    public async Task<IActionResult> CreateIgnoreRule([FromBody] CreateIgnoreRuleRequest request, CancellationToken ct)
    {
        var result = await _rules.CreateAsync(
            new CreateIgnoreRuleInput(request.Path, (IgnoreMatchType)request.MatchType, request.Hostname, request.Note),
            await _scope.GetCurrentScopeAsync(ct), ct);

        return MapMutation(result);
    }

    [HttpPut("ignore-rules/{id:int}")]
    public async Task<IActionResult> UpdateIgnoreRule(int id, [FromBody] UpdateIgnoreRuleRequest request, CancellationToken ct)
    {
        var result = await _rules.UpdateAsync(id,
            new UpdateIgnoreRuleInput(request.Path, (IgnoreMatchType)request.MatchType, request.Hostname, request.Note),
            await _scope.GetCurrentScopeAsync(ct), ct);

        return MapMutation(result);
    }

    [HttpDelete("ignore-rules/{id:int}")]
    public async Task<IActionResult> DeleteIgnoreRule(int id, CancellationToken ct)
    {
        var result = await _rules.DeleteAsync(id, await _scope.GetCurrentScopeAsync(ct), ct);
        return MapMutation(result);
    }

    [HttpPost("ignore-rules/reseed-auto-preset")]
    public async Task<IActionResult> ReseedAutoPreset(CancellationToken ct)
    {
        var scope = await _scope.GetCurrentScopeAsync(ct);
        if (!scope.HasFullAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { reason = "Full access required." });
        }
        await _seedingService.SeedAndReconcileAsync(ct);
        return NoContent();
    }

    private IActionResult MapMutation(IgnoreRuleMutation m) => m.Result switch
    {
        IgnoreRuleMutationResult.Ok => m.Entity is null ? NoContent() : Ok(MapRule(m.Entity)),
        IgnoreRuleMutationResult.NotFound => NotFound(),
        IgnoreRuleMutationResult.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { reason = m.Reason }),
        IgnoreRuleMutationResult.Conflict => Conflict(new { reason = m.Reason }),
        IgnoreRuleMutationResult.InvalidInput => BadRequest(new { reason = m.Reason }),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };

    private static IgnoreRuleItem MapRule(NotFoundIgnoreRuleEntity r) => new()
    {
        Id = r.Id,
        Hostname = r.Hostname,
        MatchType = (byte)r.MatchType,
        Path = r.Path,
        Source = (byte)r.Source,
        Note = r.Note,
        CreatedUtc = r.CreatedUtc,
        IsReadOnly = r.Source == IgnoreRuleSource.ConfigSeeded,
    };

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

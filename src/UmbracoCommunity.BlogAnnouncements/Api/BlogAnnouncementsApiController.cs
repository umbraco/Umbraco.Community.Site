using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.BlogAnnouncements.Api.Models;
using UmbracoCommunity.BlogAnnouncements.Dashboard;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;

namespace UmbracoCommunity.BlogAnnouncements.Api;

/// <summary>
/// Management API for the Blog Announcements dashboard: browse tracked posts and their delivery
/// history, inspect detection runs, view the effective config, and fire manual repost / post-now /
/// test deliveries through the same delivery leg the automatic pipeline uses.
/// </summary>
public sealed class BlogAnnouncementsApiController : BlogAnnouncementsApiControllerBase
{
    private readonly BlogAnnouncementDashboardService _service;

    public BlogAnnouncementsApiController(BlogAnnouncementDashboardService service) => _service = service;

    [HttpGet("posts")]
    [ProducesResponseType<PostListResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PostListResponse>> ListPosts(
        [FromQuery] byte? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        var query = new PostQuery(
            Status: status.HasValue ? (AnnouncementStatus)status.Value : null,
            Search: search,
            FromUtc: NormalizeUtc(from),
            ToUtc: NormalizeUtc(to),
            Skip: skip,
            Take: take);

        return Ok(await _service.ListPostsAsync(query, ct));
    }

    [HttpGet("posts/{id:guid}")]
    [ProducesResponseType<PostDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDetail>> GetPost(Guid id, CancellationToken ct)
    {
        var detail = await _service.GetPostAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("posts/{id:guid}/announce")]
    [ProducesResponseType<DeliveryResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Announce(Guid id, [FromBody] AnnounceRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<AnnouncementTrigger>(request.Trigger, ignoreCase: true, out var trigger) ||
            (trigger != AnnouncementTrigger.Repost && trigger != AnnouncementTrigger.PostNow))
        {
            return BadRequest(new { reason = "Trigger must be 'Repost' or 'PostNow'." });
        }

        var result = await _service.AnnounceAsync(id, trigger, ct);

        return result.Outcome switch
        {
            ManualAnnounceOutcome.Delivered => Ok(new DeliveryResultResponse
            {
                Outcome = OutcomeLabel(result.Delivery!),
                HttpStatus = result.Delivery!.HttpStatus,
                DryRun = result.Delivery.DryRun,
                Status = (byte?)result.Status,
                AnnouncedUtc = result.AnnouncedUtc,
            }),
            ManualAnnounceOutcome.PostNotFound => NotFound(),
            ManualAnnounceOutcome.InFlight => Conflict(new { reason = "A delivery for this post is already in flight." }),
            ManualAnnounceOutcome.InvalidStatusForPostNow => BadRequest(new
            {
                reason = "Post now is only available for posts that were never announced (Pending / SkippedTooOld).",
            }),
            _ => BadRequest(new { reason = "Trigger must be 'Repost' or 'PostNow'." }),
        };
    }

    /// <summary>
    /// Marks a post as not announced (testing aid): resets the row to Pending so the automatic
    /// cycle can pick it up again, keeping the row and its full attempt history.
    /// </summary>
    [HttpPost("posts/{id:guid}/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reset(Guid id, CancellationToken ct)
    {
        var outcome = await _service.ResetAsync(id, ct);
        return outcome switch
        {
            ResetOutcome.Reset => NoContent(),
            ResetOutcome.PostNotFound => NotFound(),
            ResetOutcome.InFlight => Conflict(new { reason = "A delivery for this post is already in flight." }),
            _ => BadRequest(new { reason = "Only announced or failed posts can be marked as not announced." }),
        };
    }

    [HttpGet("runs")]
    [ProducesResponseType<RunListResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RunListResponse>> ListRuns(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
        => Ok(await _service.ListRunsAsync(skip, take, ct));

    [HttpGet("settings")]
    [ProducesResponseType<SettingsResponse>(StatusCodes.Status200OK)]
    public ActionResult<SettingsResponse> GetSettings() => Ok(_service.GetSettings());

    /// <summary>
    /// Triggers one full poll cycle on demand (fetch new posts, refresh caches, run announcement
    /// detection) instead of waiting for the periodic timer.
    /// </summary>
    [HttpPost("poll")]
    [ProducesResponseType<PollNowResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> PollNow(CancellationToken ct)
    {
        var result = await _service.PollNowAsync(ct);
        return result.Outcome switch
        {
            PollNowOutcome.Completed => Ok(new PollNowResponse { Run = result.Run }),
            PollNowOutcome.InFlight => Conflict(new { reason = "A poll is already running." }),
            _ => StatusCode(StatusCodes.Status501NotImplemented,
                new { reason = "The host application has not registered a feed poller." }),
        };
    }

    [HttpPost("test-message")]
    [ProducesResponseType<DeliveryResultResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeliveryResultResponse>> SendTestMessage(CancellationToken ct)
    {
        var result = await _service.SendTestMessageAsync(ct);
        return Ok(new DeliveryResultResponse
        {
            Outcome = OutcomeLabel(result),
            HttpStatus = result.HttpStatus,
            DryRun = result.DryRun,
        });
    }

    private static string OutcomeLabel(DeliveryResult r) => r.DryRun ? "DryRun" : r.Success ? "Success" : "Failed";

    /// <summary>Treats an incoming date filter as UTC (the tracking store is UTC).</summary>
    private static DateTime? NormalizeUtc(DateTime? value) =>
        value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;
}

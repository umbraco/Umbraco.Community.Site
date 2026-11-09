using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.ProfileMatchReview.Api.Models;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.ProfileMatchReview.Api;

/// <summary>Review of the activity members add to their profile by hand. Same reviewers as the match review.</summary>
public sealed class ProfileActivityReviewApiController : ProfileMatchReviewApiControllerBase
{
    private readonly ProfileActivityEntryService _activityEntryService;

    public ProfileActivityReviewApiController(ProfileActivityEntryService activityEntryService, IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
        : base(backOfficeSecurityAccessor)
    {
        _activityEntryService = activityEntryService;
    }

    /// <summary>Entries in one state (pending by default), newest first.</summary>
    [HttpGet("activity")]
    [ProducesResponseType<ActivityEntryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ActivityEntryListResponse>> List(
        ActivityEntryState state = ActivityEntryState.Pending,
        int skip = 0,
        int take = 25,
        CancellationToken ct = default)
    {
        if (!IsReviewer)
        {
            return NotReviewer();
        }

        var page = await _activityEntryService.ListAsync(state, skip, take, ct);
        return new ActivityEntryListResponse(page.Items.Select(ToResponse).ToList(), page.Total);
    }

    /// <summary>Verifies an entry: it shows on the member's profile from now on.</summary>
    [HttpPost("activity/{id:int}/verify")]
    [ProducesResponseType<ActivityEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityEntryResponse>> Verify(int id, CancellationToken ct = default)
    {
        if (!IsReviewer)
        {
            return NotReviewer();
        }

        var entry = await _activityEntryService.VerifyAsync(id, CurrentUser?.Key, CurrentUser?.Name, ct);
        return entry == null ? NotFound() : ToResponse(entry);
    }

    /// <summary>Rejects an entry with a note the member can read.</summary>
    [HttpPost("activity/{id:int}/reject")]
    [ProducesResponseType<ActivityEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityEntryResponse>> Reject(int id, [FromBody] RejectActivityEntryRequest request, CancellationToken ct = default)
    {
        if (!IsReviewer)
        {
            return NotReviewer();
        }

        var reason = ProfileText.Normalize(request.Reason);
        if (reason == null || reason.Length > ProfileActivityEntry.MaxReasonLength)
        {
            return Problem(
                title: "Reason needed",
                detail: $"Give the member a reason of up to {ProfileActivityEntry.MaxReasonLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var entry = await _activityEntryService.RejectAsync(id, reason, CurrentUser?.Key, CurrentUser?.Name, ct);
        return entry == null ? NotFound() : ToResponse(entry);
    }

    private static ActivityEntryResponse ToResponse(ActivityEntryRecord entry) =>
        new(
            entry.Id,
            entry.GitHubHandle,
            entry.DisplayName,
            entry.Kind,
            ProfileActivityEntry.KindLabel(entry.Kind),
            entry.Title,
            entry.Url,
            entry.Date,
            entry.Description,
            entry.State.ToString(),
            entry.RejectionReason,
            entry.DecidedByUserName,
            entry.DecidedUtc,
            entry.CreatedUtc);
}

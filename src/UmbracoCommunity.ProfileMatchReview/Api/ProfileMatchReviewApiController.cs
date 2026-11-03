using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.ProfileMatchReview.Api.Models;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Matching;

namespace UmbracoCommunity.ProfileMatchReview.Api;

public sealed class ProfileMatchReviewApiController : ProfileMatchReviewApiControllerBase
{
    private readonly ProfileMatchStore _store;

    public ProfileMatchReviewApiController(ProfileMatchStore store, IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
        : base(backOfficeSecurityAccessor)
    {
        _store = store;
    }

    /// <summary>Whether the signed-in user may review matches; the dashboard uses it to hide itself.</summary>
    [HttpGet("access")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult Access() => IsReviewer ? NoContent() : NotReviewer();

    /// <summary>Matches in one state (held by default), newest first.</summary>
    [HttpGet("matches")]
    [ProducesResponseType<ProfileMatchListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProfileMatchListResponse>> List(
        ProfileMatchState state = ProfileMatchState.Held,
        int skip = 0,
        int take = 25,
        CancellationToken ct = default)
    {
        if (!IsReviewer)
        {
            return NotReviewer();
        }

        var page = await _store.ListAsync(state, skip, take, ct);
        return new ProfileMatchListResponse(page.Items.Select(ToResponse).ToList(), page.Total);
    }

    /// <summary>Confirms a match: the source belongs to the member. A later automatic run does not change it.</summary>
    [HttpPost("matches/{id:int}/link")]
    [ProducesResponseType<ProfileMatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProfileMatchResponse>> Link(int id, CancellationToken ct = default) =>
        DecideAsync(id, ProfileMatchState.Confirmed, ct);

    /// <summary>Rejects a match: the source does not belong to the member. A later automatic run does not change it.</summary>
    [HttpPost("matches/{id:int}/reject")]
    [ProducesResponseType<ProfileMatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProfileMatchResponse>> Reject(int id, CancellationToken ct = default) =>
        DecideAsync(id, ProfileMatchState.Rejected, ct);

    private async Task<ActionResult<ProfileMatchResponse>> DecideAsync(int id, ProfileMatchState state, CancellationToken ct)
    {
        if (!IsReviewer)
        {
            return NotReviewer();
        }

        var record = await _store.DecideManuallyAsync(id, state, CurrentUser?.Key, CurrentUser?.Name, ct);
        return record == null ? NotFound() : ToResponse(record);
    }

    private static ProfileMatchResponse ToResponse(ProfileMatchRecord record) =>
        new(
            record.Id,
            record.GitHubHandle,
            record.DisplayName,
            record.SourceUrl,
            record.State.ToString(),
            record.Confidence,
            record.Signals.Select(s => new MatchSignalResponse(s.Name, s.Detail)).ToList(),
            record.DecidedBy.ToString(),
            record.DecidedByUserName,
            record.DecidedUtc);
}

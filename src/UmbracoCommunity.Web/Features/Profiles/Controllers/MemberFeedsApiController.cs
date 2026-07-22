using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Features.Profiles.Data;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Feed management API — always scoped to the current signed-in member, never trusting an
/// id-in-URL for whose feeds are being modified. Shared between the onboarding wizard's
/// feeds step and the Account page (same endpoints, same behaviour in both places).
/// </summary>
[ApiController]
[Route("api/member-feeds")]
public class MemberFeedsApiController : ControllerBase
{
    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly IProfileSyncClient _profileSyncClient;

    public MemberFeedsApiController(
        IMemberManager memberManager,
        MemberProfileStore store,
        IProfileSyncClient profileSyncClient)
    {
        _memberManager = memberManager;
        _store = store;
        _profileSyncClient = profileSyncClient;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var feeds = await _store.GetActiveFeedsAsync(member.Key, cancellationToken);
        return Ok(feeds.Select(ToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddFeedRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Platform))
        {
            return BadRequest(new { error = "Please provide a platform name." });
        }

        if (!IsValidFeedUrl(request.Url))
        {
            return BadRequest(new { error = "Please provide a valid absolute http or https URL." });
        }

        var feed = await _store.AddFeedAsync(member.Key, request.Platform.Trim(), request.Url.Trim(), cancellationToken);
        if (feed == null)
        {
            return BadRequest(new { error = "Start onboarding before adding feeds." });
        }

        await _profileSyncClient.NotifyFeedAddedAsync(member.UserName ?? string.Empty, feed.Platform, feed.Url, cancellationToken);

        return Ok(ToResponse(feed));
    }

    [HttpPut("{id:int}/hide")]
    public Task<IActionResult> Hide(int id, CancellationToken cancellationToken) => SetHidden(id, true, cancellationToken);

    [HttpPut("{id:int}/unhide")]
    public Task<IActionResult> Unhide(int id, CancellationToken cancellationToken) => SetHidden(id, false, cancellationToken);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, [FromBody] RemoveFeedRequest? request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var feeds = await _store.GetActiveFeedsAsync(member.Key, cancellationToken);
        var feed = feeds.FirstOrDefault(f => f.Id == id);
        if (feed == null)
        {
            return NotFound();
        }

        var removed = await _store.RemoveFeedAsync(member.Key, id, request?.Reason, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        await _profileSyncClient.NotifyFeedRemovedAsync(member.UserName ?? string.Empty, feed.Url, request?.Reason, cancellationToken);

        return Ok();
    }

    private async Task<IActionResult> SetHidden(int id, bool isHidden, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var feeds = await _store.GetActiveFeedsAsync(member.Key, cancellationToken);
        var feed = feeds.FirstOrDefault(f => f.Id == id);
        if (feed == null)
        {
            return NotFound();
        }

        var updated = await _store.SetFeedHiddenAsync(member.Key, id, isHidden, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        await _profileSyncClient.NotifyFeedHiddenAsync(member.UserName ?? string.Empty, feed.Url, isHidden, cancellationToken);

        return Ok();
    }

    private static bool IsValidFeedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static MemberFeedResponse ToResponse(MemberFeedEntity feed) => new(feed.Id, feed.Platform, feed.Url, feed.IsHidden, feed.Source.ToString());
}

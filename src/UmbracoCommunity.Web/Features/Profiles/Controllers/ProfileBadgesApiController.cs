using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.ForumBadges;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Lets the signed-in member choose which of their Umbraco forum badges their profile shows (see
/// <see cref="ForumBadgeService"/>). The forum account is the one their own forum link points at; only that user's
/// public badges are read. Always scoped to the current member, never trusting an id from the caller, and only once
/// they have finished onboarding. Every PUT must carry the antiforgery request token from <c>api/antiforgery/token</c>
/// in the <c>RequestVerificationToken</c> header.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/profile/badges")]
public class ProfileBadgesApiController : ControllerBase
{
    private const string NoProfileError = "Finish setting up your profile first.";
    private const string NoForumLinkError = "Add a link to your forum profile under Feeds & links first.";
    private const string NotFoundError = "We couldn't find that user on the forum. Check the link to your forum profile under Feeds & links.";
    private const string UnavailableError = "We couldn't reach the forum just now. Please try again in a few minutes.";

    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly ForumBadgeService _badges;

    public ProfileBadgesApiController(IMemberManager memberManager, MemberProfileStore store, ForumBadgeService badges)
    {
        _memberManager = memberManager;
        _store = store;
        _badges = badges;
    }

    /// <summary>The member's forum badges, which of them their profile shows, and how many it can show.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var entity = await _store.GetByMemberKeyAsync(member.Key, cancellationToken);
        if (entity?.OnboardingStatus != OnboardingStatus.Completed)
        {
            return BadRequest(new { error = NoProfileError });
        }

        var (username, result) = await _badges.GetMemberBadgesAsync(entity, cancellationToken);
        return Ok(ToResponse(username, result, entity.ShownForumBadgeIds));
    }

    /// <summary>
    /// Replaces the badges the profile shows. Ids of badges the member doesn't hold are dropped; more than
    /// <see cref="ForumBadgeService.MaxShown"/> is refused. An empty list shows none.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileBadgesRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var entity = await _store.GetByMemberKeyAsync(member.Key, cancellationToken);
        if (entity?.OnboardingStatus != OnboardingStatus.Completed)
        {
            return BadRequest(new { error = NoProfileError });
        }

        var (username, result) = await _badges.GetMemberBadgesAsync(entity, cancellationToken);
        if (username == null)
        {
            return BadRequest(new { error = NoForumLinkError });
        }

        switch (result.Status)
        {
            case ForumBadgesStatus.NotFound:
                return BadRequest(new { error = NotFoundError });
            case ForumBadgesStatus.Unavailable:
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = UnavailableError });
        }

        if (!ForumBadgeService.TryNormalizeChoice(request.BadgeIds, result.Badges, out var ids, out var error))
        {
            return BadRequest(new { error });
        }

        var saved = await _store.SetShownForumBadgesAsync(member.Key, ids, cancellationToken);
        if (saved == null)
        {
            return BadRequest(new { error = NoProfileError });
        }

        return Ok(ToResponse(username, result, saved.ShownForumBadgeIds));
    }

    private ProfileBadgesResponse ToResponse(string? username, ForumBadgesResult result, IReadOnlyCollection<int> chosenIds)
    {
        var status = username == null
            ? "noForumLink"
            : result.Status switch
            {
                ForumBadgesStatus.Ok => "ok",
                ForumBadgesStatus.NotFound => "notFound",
                _ => "unavailable",
            };

        var shown = ForumBadgeService.Shown(result.Badges, chosenIds).Select(b => b.Id).ToHashSet();
        var badges = result.Badges
            .Select(b => new ProfileBadgeResponse(
                b.Id,
                b.Name,
                b.Description,
                _badges.ToProfileBadge(b).IconUrl,
                b.BadgeType,
                b.GrantCount,
                b.GrantedAt,
                shown.Contains(b.Id),
                b.Url))
            .ToList();

        return new ProfileBadgesResponse(
            status,
            username,
            username != null ? ForumProfileUrl.ProfileUrl(username) : null,
            badges,
            badges.Where(b => b.Shown).Select(b => b.Id).ToList(),
            ForumBadgeService.MaxShown);
    }
}

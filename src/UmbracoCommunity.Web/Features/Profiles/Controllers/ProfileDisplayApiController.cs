using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Lets the signed-in member choose what the activity sections of their own profile show (see
/// <see cref="ProfileDisplay"/>). Always scoped to the current member, never trusting an id from the caller,
/// and only for members who are offered the choices (see <see cref="ProfileAccessService.CanChooseProfileDisplayAsync"/>).
/// Every PUT must carry the antiforgery request token from <c>api/antiforgery/token</c> in the
/// <c>RequestVerificationToken</c> header.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/profile/display")]
public class ProfileDisplayApiController : ControllerBase
{
    private const string NotOfferedError = "Choosing what your profile shows isn't available to you yet.";
    private const string NoProfileError = "Finish setting up your profile first.";

    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly ProfileAccessService _profileAccessService;
    private readonly IProfileDataProvider _profileDataProvider;
    private readonly ProfileActivityService _profileActivityService;

    public ProfileDisplayApiController(
        IMemberManager memberManager,
        MemberProfileStore store,
        ProfileAccessService profileAccessService,
        IProfileDataProvider profileDataProvider,
        ProfileActivityService profileActivityService)
    {
        _memberManager = memberManager;
        _store = store;
        _profileAccessService = profileAccessService;
        _profileDataProvider = profileDataProvider;
        _profileActivityService = profileActivityService;
    }

    /// <summary>The member's activity sections with every item in them, hidden ones included, and their stored choices.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member?.UserName == null)
        {
            return Unauthorized();
        }

        if (!await _profileAccessService.CanChooseProfileDisplayAsync())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = NotOfferedError });
        }

        var entity = await _store.GetByMemberKeyAsync(member.Key, cancellationToken);
        if (entity?.OnboardingStatus != OnboardingStatus.Completed)
        {
            return BadRequest(new { error = NoProfileError });
        }

        var items = await ActivityItemsAsync(entity, cancellationToken);

        var sections = PlatformActivityGroups.All
            .Select(group => new ProfileDisplaySection(
                group,
                ProfileDisplay.SectionLabel(group),
                items
                    .Where(i => i.Group == group)
                    .Select(i => new ProfileDisplayItem(i.ActivityId, i.Title, i.Date))
                    .ToList()))
            .ToList();

        return Ok(new ProfileDisplayResponse(sections, ToChoicesResponse(ProfileDisplay.StoredChoices(entity))));
    }

    /// <summary>
    /// Replaces the member's choices: sections switched off, hidden items, and pinned items in order. A pinned
    /// item must be in the member's activity, or already be pinned.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileDisplayRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (!await _profileAccessService.CanChooseProfileDisplayAsync())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = NotOfferedError });
        }

        if (!ProfileDisplay.TryNormalize(request, out var choices, out var error))
        {
            return BadRequest(new { error });
        }

        var existing = await _store.GetByMemberKeyAsync(member.Key, cancellationToken);
        if (existing == null)
        {
            return BadRequest(new { error = NoProfileError });
        }

        // Each pinned item is kept with its pin, so it stays featured once it drops out of the latest activity.
        if (!ProfileDisplay.TryTakeSnapshots(
                choices,
                await ActivityItemsAsync(existing, cancellationToken),
                ProfileDisplay.StoredChoices(existing),
                out var withSnapshots,
                out error))
        {
            return BadRequest(new { error });
        }

        var entity = await _store.SetDisplayChoicesAsync(member.Key, withSnapshots, cancellationToken);
        if (entity == null)
        {
            return BadRequest(new { error = NoProfileError });
        }

        return Ok(ToChoicesResponse(ProfileDisplay.StoredChoices(entity)));
    }

    /// <summary>
    /// Every item in the member's activity sections. The owner's own request, so a hidden profile is included.
    /// Choices are not applied: the member needs to see hidden items to show them again.
    /// </summary>
    private async Task<IReadOnlyList<ProfileFeaturedItem>> ActivityItemsAsync(MemberProfileEntity entity, CancellationToken cancellationToken)
    {
        var profile = await _profileDataProvider.GetProfileAsync(entity.GitHubHandle, includeHidden: true, cancellationToken);
        return profile != null
            ? ProfileDisplay.ActivityItems(await _profileActivityService.EnrichAsync(profile, cancellationToken)).ToList()
            : [];
    }

    private static ProfileDisplayChoicesResponse ToChoicesResponse(ProfileDisplayChoices choices) =>
        new(
            choices.HiddenSections,
            choices.HiddenActivityIds,
            choices.Pins.Select(p => new ProfilePinResponse(p.ActivityId, p.Reason, p.Snapshot?.Title)).ToList());
}

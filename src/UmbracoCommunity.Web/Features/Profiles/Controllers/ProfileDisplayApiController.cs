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
    private readonly ProfileTimelinePreviewService _timelinePreviewService;

    public ProfileDisplayApiController(
        IMemberManager memberManager,
        MemberProfileStore store,
        ProfileAccessService profileAccessService,
        IProfileDataProvider profileDataProvider,
        ProfileActivityService profileActivityService,
        ProfileTimelinePreviewService timelinePreviewService)
    {
        _memberManager = memberManager;
        _store = store;
        _profileAccessService = profileAccessService;
        _profileDataProvider = profileDataProvider;
        _profileActivityService = profileActivityService;
        _timelinePreviewService = timelinePreviewService;
    }

    /// <summary>
    /// The member's activity sections with every item in them, hidden ones included, and their stored choices. Each item
    /// carries what the profile's timeline draws for it (icon, link, meta line, preview card, month), and items are in
    /// timeline order, newest first.
    /// </summary>
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

        var profile = await EnrichedProfileAsync(entity, cancellationToken);
        var activity = profile != null ? ProfileDisplay.ActivityItems(profile).ToList() : [];

        // The same timeline entries the profile page renders, so the account page draws each item as the profile does.
        // Only the member's own items are kept: those are the ones that can be hidden or pinned.
        var ids = activity.Select(i => i.ActivityId).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<ProfileTimelineEntry> timeline = profile != null
            ? ProfileTimeline.Entries(profile).Where(e => e.ActivityId != null && ids.Contains(e.ActivityId)).ToList()
            : [];

        // Outside work is held to the same per-render limit and time budget as a profile page, newest entries first;
        // the rest show as plain lines until a later visit has their cards ready.
        timeline = await _timelinePreviewService.AddCardsAsync(timeline, cancellationToken);

        var items = ProfileDisplay.DisplayItems(activity, timeline);
        var sections = PlatformActivityGroups.All
            .Select(group => new ProfileDisplaySection(
                group,
                ProfileDisplay.SectionLabel(group),
                items.Where(i => i.Group == group).ToList()))
            .ToList();

        return Ok(new ProfileDisplayResponse(sections, ToChoicesResponse(ProfileDisplay.StoredChoices(entity))));
    }

    /// <summary>
    /// Replaces the member's choices: sections switched off, hidden items, pinned items in order, the activity graph
    /// switch and the timeline's preview card choices. A pinned item must be in the member's activity, or already be pinned.
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
    private async Task<IReadOnlyList<ProfileFeaturedItem>> ActivityItemsAsync(MemberProfileEntity entity, CancellationToken cancellationToken) =>
        await EnrichedProfileAsync(entity, cancellationToken) is { } profile
            ? ProfileDisplay.ActivityItems(profile).ToList()
            : [];

    /// <summary>The member's profile with its activity loaded, choices not applied. The owner's own request, so a hidden profile is included.</summary>
    private async Task<CommunityProfile?> EnrichedProfileAsync(MemberProfileEntity entity, CancellationToken cancellationToken)
    {
        var profile = await _profileDataProvider.GetProfileAsync(entity.GitHubHandle, includeHidden: true, cancellationToken: cancellationToken);
        return profile != null ? await _profileActivityService.EnrichAsync(profile, cancellationToken) : null;
    }

    private static ProfileDisplayChoicesResponse ToChoicesResponse(ProfileDisplayChoices choices) =>
        new(
            choices.HiddenSections,
            choices.HiddenActivityIds,
            choices.Pins.Select(p => new ProfilePinResponse(p.ActivityId, p.Reason, p.Snapshot?.Title)).ToList());
}

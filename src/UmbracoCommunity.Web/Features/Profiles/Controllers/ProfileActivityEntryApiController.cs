using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Lets the signed-in member add activity to their own profile by hand and withdraw it again (see
/// <see cref="ProfileActivityEntry"/>). An entry waits for an admin before it shows. Always scoped to the current member,
/// never trusting an id from the caller, and only for members who are offered it (see
/// <see cref="ProfileAccessService.CanAddActivityAsync"/>). Every POST and DELETE must carry the antiforgery request
/// token from <c>api/antiforgery/token</c> in the <c>RequestVerificationToken</c> header.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/profile/activity")]
public class ProfileActivityEntryApiController : ControllerBase
{
    private const string NotOfferedError = "Adding activity to your profile isn't available to you yet.";
    private const string NoProfileError = "Finish setting up your profile first.";

    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _profileStore;
    private readonly ProfileAccessService _profileAccessService;
    private readonly ProfileActivityEntryService _activityEntryService;

    public ProfileActivityEntryApiController(
        IMemberManager memberManager,
        MemberProfileStore profileStore,
        ProfileAccessService profileAccessService,
        ProfileActivityEntryService activityEntryService)
    {
        _memberManager = memberManager;
        _profileStore = profileStore;
        _profileAccessService = profileAccessService;
        _activityEntryService = activityEntryService;
    }

    /// <summary>The member's entries that are waiting, shown or rejected, newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        var entries = await _activityEntryService.ListForMemberAsync(profile.Id, cancellationToken);
        return Ok(entries.Select(ToResponse).ToList());
    }

    /// <summary>Adds an entry that waits for an admin to check it.</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddActivityEntryRequest request, CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        if (!ProfileActivityEntry.TryNormalize(request, DateOnly.FromDateTime(DateTime.UtcNow), out var entry, out var error))
        {
            return BadRequest(new { error });
        }

        var added = await _activityEntryService.AddAsync(profile.Id, entry, cancellationToken);
        return added == null
            ? Conflict(new { error = "That link is already added, or you have too many entries waiting to be checked." })
            : Ok(ToResponse(added));
    }

    /// <summary>Withdraws one of the member's own entries; one that was already shown stops showing at once.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        return await _activityEntryService.WithdrawAsync(profile.Id, id, cancellationToken) == null
            ? NotFound(new { error = "That entry can't be withdrawn." })
            : NoContent();
    }

    private async Task<(MemberProfileEntity? Profile, IActionResult? Failure)> ResolveProfileAsync(CancellationToken cancellationToken = default)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return (null, Unauthorized());
        }

        if (!await _profileAccessService.CanAddActivityAsync())
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden, new { error = NotOfferedError }));
        }

        var profile = await _profileStore.GetByMemberKeyAsync(member.Key, cancellationToken);
        return profile?.OnboardingStatus == OnboardingStatus.Completed
            ? (profile, null)
            : (null, BadRequest(new { error = NoProfileError }));
    }

    private static MemberActivityEntryResponse ToResponse(ActivityEntryRecord entry) =>
        new(
            entry.Id,
            entry.Kind,
            ProfileActivityEntry.KindLabel(entry.Kind),
            entry.Title,
            entry.Url,
            entry.Date,
            entry.Description,
            entry.State.ToString(),
            entry.RejectionReason);
}

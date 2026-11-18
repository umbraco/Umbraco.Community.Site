using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Onboarding + ongoing profile-editing API. Covers both the onboarding wizard and later
/// Account-page edits (avatar/name/headline/bio are the same operation regardless of when they happen) —
/// always scoped to the member <see cref="ProfileEditSubjectResolver"/> resolves (the signed-in member, or the
/// member a profile admin edits; onboarding is the signed-in member's own only), never trusting an id from the caller.
/// Every POST, PUT and DELETE must carry the antiforgery request token from
/// <c>api/antiforgery/token</c> in the <c>RequestVerificationToken</c> header.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[ProfileEditAudit]
[Route("api/profile")]
public class ProfileApiController : ControllerBase
{
    private readonly ProfileEditSubjectResolver _subjects;
    private readonly MemberProfileStore _store;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;
    private readonly AvatarUploadService _avatarUploadService;
    private readonly IProfileSyncClient _profileSyncClient;
    private readonly GitHubProfileDefaultsService _gitHubDefaultsService;

    public ProfileApiController(
        ProfileEditSubjectResolver subjects,
        MemberProfileStore store,
        ProfileAvatarUrlResolver avatarUrlResolver,
        AvatarUploadService avatarUploadService,
        IProfileSyncClient profileSyncClient,
        GitHubProfileDefaultsService gitHubDefaultsService)
    {
        _subjects = subjects;
        _store = store;
        _avatarUrlResolver = avatarUrlResolver;
        _avatarUploadService = avatarUploadService;
        _profileSyncClient = profileSyncClient;
        _gitHubDefaultsService = gitHubDefaultsService;
    }

    /// <summary>Idempotently creates/fetches the current member's profile row and returns its current draft state.</summary>
    [HttpPost("onboarding/start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveSelfOnlyAsync(Request);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult(ProfileEditSubjectResult.SelfOnlyError)!;
        }

        if (subject.Handle.Length == 0)
        {
            return Unauthorized();
        }


        var entity = await _store.GetOrStartOnboardingAsync(subject.MemberKey, subject.Handle, subject.SignedInMember.Name ?? subject.Handle, cancellationToken);
        await _gitHubDefaultsService.EnsureGitHubDefaultsAsync(entity, cancellationToken);

        return Ok(new OnboardingStateResponse(
            entity.GitHubHandle,
            entity.EffectiveDisplayName,
            entity.Bio,
            _avatarUrlResolver.Resolve(entity.AvatarMediaKey, entity.GitHubHandle),
            entity.AvatarMediaKey.HasValue,
            entity.OnboardingStatus.ToString(),
            entity.GitHubBio));
    }

    /// <summary>Sets the member's own bio. Empty or whitespace-only text clears it, so the GitHub bio shows again.</summary>
    [HttpPut("bio")]
    public async Task<IActionResult> UpdateBio([FromBody] UpdateBioRequest request, CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult()!;
        }

        if (ProfileText.Normalize(request.Bio) is { Length: > ProfileText.MaxBioLength })
        {
            return BadRequest(new { error = $"Bio must be {ProfileText.MaxBioLength} characters or fewer." });
        }

        var entity = await _store.UpdateBioAsync(subject.MemberKey, request.Bio, cancellationToken);
        if (entity == null)
        {
            return BadRequest(new { error = "Onboarding hasn't been started yet." });
        }

        return Ok(ToDetailsResponse(entity));
    }

    /// <summary>Sets the member's own headline. Empty or whitespace-only text clears it, so the GitHub company shows again.</summary>
    [HttpPut("headline")]
    public async Task<IActionResult> UpdateHeadline([FromBody] UpdateHeadlineRequest request, CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult()!;
        }

        if (ProfileText.Normalize(request.Headline) is { Length: > ProfileText.MaxHeadlineLength })
        {
            return BadRequest(new { error = $"Headline must be {ProfileText.MaxHeadlineLength} characters or fewer." });
        }

        var entity = await _store.UpdateHeadlineAsync(subject.MemberKey, request.Headline, cancellationToken);
        if (entity == null)
        {
            return BadRequest(new { error = "Onboarding hasn't been started yet." });
        }

        return Ok(ToDetailsResponse(entity));
    }

    /// <summary>Sets the member's own display name. Empty or whitespace-only text clears it, so the GitHub name shows again.</summary>
    [HttpPut("display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request, CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult()!;
        }

        if (ProfileText.Normalize(request.DisplayName) is { Length: > ProfileText.MaxDisplayNameLength })
        {
            return BadRequest(new { error = $"Display name must be {ProfileText.MaxDisplayNameLength} characters or fewer." });
        }

        var entity = await _store.UpdateDisplayNameAsync(subject.MemberKey, request.DisplayName, cancellationToken);
        if (entity == null)
        {
            return BadRequest(new { error = "Onboarding hasn't been started yet." });
        }

        return Ok(ToDetailsResponse(entity));
    }

    /// <summary>
    /// Shows or hides the current member's profile. To everyone but its owner, a hidden profile
    /// renders as if the handle had no profile at all.
    /// </summary>
    [HttpPut("visibility")]
    public async Task<IActionResult> UpdateVisibility([FromBody] UpdateProfileVisibilityRequest request, CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult()!;
        }

        var updated = await _store.SetProfileVisibleAsync(subject.MemberKey, request.Visible, cancellationToken);
        if (!updated)
        {
            return BadRequest(new { error = "Onboarding hasn't been started yet." });
        }

        return Ok();
    }

    [HttpPut("avatar")]
    [RequestSizeLimit(AvatarUploadService.MaxBytes)]
    public async Task<IActionResult> UpdateAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult()!;
        }

        if (file.Length == 0)
        {
            return BadRequest(new { error = "No file was uploaded." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _avatarUploadService.UploadAsync(subject.MemberKey, file.FileName, file.Length, stream, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new UpdateAvatarResponse(_avatarUrlResolver.Resolve(result.MediaKey, subject.Handle), true));
    }

    /// <summary>Removes the member's uploaded avatar, reverting display to their GitHub default.</summary>
    [HttpDelete("avatar")]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult()!;
        }

        await _avatarUploadService.RemoveAsync(subject.MemberKey, cancellationToken);

        return Ok(new UpdateAvatarResponse(_avatarUrlResolver.Resolve(null, subject.Handle), false));
    }

    /// <summary>
    /// Marks onboarding complete. The profile still starts hidden: only its owner can see it
    /// until they show it (see <see cref="UpdateVisibility"/>).
    /// </summary>
    [HttpPost("onboarding/complete")]
    public async Task<IActionResult> Complete(CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveSelfOnlyAsync(Request);
        if (resolved.Subject is not { } subject)
        {
            return resolved.FailureResult(ProfileEditSubjectResult.SelfOnlyError)!;
        }

        var entity = await _store.CompleteOnboardingAsync(subject.MemberKey, cancellationToken);
        if (entity == null)
        {
            return BadRequest(new { error = "Onboarding hasn't been started yet." });
        }

        // Best-effort — the local write above is always the source of truth and already
        // committed regardless of whether the platform's (currently stubbed) sync succeeds.
        await _profileSyncClient.NotifyProfileClaimedAsync(entity.GitHubHandle, entity.PlatformProfileId, cancellationToken);

        return Ok();
    }

    private static ProfileDetailsResponse ToDetailsResponse(MemberProfileEntity entity) =>
        new(
            entity.EffectiveDisplayName,
            entity.DisplayNameOverride != null,
            entity.DisplayName,
            entity.EffectiveHeadline,
            entity.Headline != null,
            entity.GitHubCompany,
            entity.EffectiveBio,
            entity.Bio != null,
            entity.GitHubBio);
}

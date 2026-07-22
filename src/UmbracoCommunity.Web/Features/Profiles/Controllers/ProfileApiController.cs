using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Onboarding + ongoing profile-editing API. Covers both the onboarding wizard and later
/// Account-page edits (avatar/bio are the same operation regardless of when they happen) —
/// always scoped to the current signed-in member, never trusting an id from the caller.
/// </summary>
[ApiController]
[Route("api/profile")]
public class ProfileApiController : ControllerBase
{
    // Short by design — the bio renders in the profile page's hero band next to the headline
    // (see community-profile.css .community-profile__bio, max-width: 60ch), not as a full
    // "about me" section, so it needs to stay skimmable rather than wrapping for many lines.
    private const int MaxBioLength = 280;

    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;
    private readonly AvatarUploadService _avatarUploadService;
    private readonly IProfileSyncClient _profileSyncClient;

    public ProfileApiController(
        IMemberManager memberManager,
        MemberProfileStore store,
        ProfileAvatarUrlResolver avatarUrlResolver,
        AvatarUploadService avatarUploadService,
        IProfileSyncClient profileSyncClient)
    {
        _memberManager = memberManager;
        _store = store;
        _avatarUrlResolver = avatarUrlResolver;
        _avatarUploadService = avatarUploadService;
        _profileSyncClient = profileSyncClient;
    }

    /// <summary>Idempotently creates/fetches the current member's profile row and returns its current draft state.</summary>
    [HttpPost("onboarding/start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member?.UserName == null)
        {
            return Unauthorized();
        }

        var entity = await _store.GetOrStartOnboardingAsync(member.Key, member.UserName, member.Name ?? member.UserName, cancellationToken);

        return Ok(new OnboardingStateResponse(
            entity.GitHubHandle,
            entity.DisplayName,
            entity.Bio,
            _avatarUrlResolver.Resolve(entity.AvatarMediaKey, entity.GitHubHandle),
            entity.AvatarMediaKey.HasValue,
            entity.OnboardingStatus.ToString()));
    }

    [HttpPut("bio")]
    public async Task<IActionResult> UpdateBio([FromBody] UpdateBioRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (request.Bio is { Length: > MaxBioLength })
        {
            return BadRequest(new { error = $"Bio must be {MaxBioLength} characters or fewer." });
        }

        await _store.UpdateBioAsync(member.Key, request.Bio, cancellationToken);
        return Ok();
    }

    [HttpPut("avatar")]
    [RequestSizeLimit(AvatarUploadService.MaxBytes)]
    public async Task<IActionResult> UpdateAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (file.Length == 0)
        {
            return BadRequest(new { error = "No file was uploaded." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _avatarUploadService.UploadAsync(member.Key, file.FileName, file.Length, stream, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new UpdateAvatarResponse(_avatarUrlResolver.Resolve(result.MediaKey, member.UserName ?? string.Empty), true));
    }

    /// <summary>Removes the member's uploaded avatar, reverting display to their GitHub default.</summary>
    [HttpDelete("avatar")]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        await _avatarUploadService.RemoveAsync(member.Key, cancellationToken);

        return Ok(new UpdateAvatarResponse(_avatarUrlResolver.Resolve(null, member.UserName ?? string.Empty), false));
    }

    /// <summary>Marks onboarding complete — this is what makes the profile publicly visible.</summary>
    [HttpPost("onboarding/complete")]
    public async Task<IActionResult> Complete(CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var entity = await _store.CompleteOnboardingAsync(member.Key, cancellationToken);
        if (entity == null)
        {
            return BadRequest(new { error = "Onboarding hasn't been started yet." });
        }

        // Best-effort — the local write above is always the source of truth and already
        // committed regardless of whether the platform's (currently stubbed) sync succeeds.
        await _profileSyncClient.NotifyProfileClaimedAsync(entity.GitHubHandle, entity.PlatformProfileId, cancellationToken);

        return Ok();
    }
}

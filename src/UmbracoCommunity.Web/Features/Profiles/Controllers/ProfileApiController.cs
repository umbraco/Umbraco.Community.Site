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
    private const int MaxBioLength = 1000;

    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;
    private readonly AvatarUploadService _avatarUploadService;
    private readonly ISphereProfileSyncClient _sphereSyncClient;

    public ProfileApiController(
        IMemberManager memberManager,
        MemberProfileStore store,
        ProfileAvatarUrlResolver avatarUrlResolver,
        AvatarUploadService avatarUploadService,
        ISphereProfileSyncClient sphereSyncClient)
    {
        _memberManager = memberManager;
        _store = store;
        _avatarUrlResolver = avatarUrlResolver;
        _avatarUploadService = avatarUploadService;
        _sphereSyncClient = sphereSyncClient;
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
    [RequestSizeLimit(5_000_000)]
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

        return Ok(new UpdateAvatarResponse(_avatarUrlResolver.Resolve(result.MediaKey, member.UserName ?? string.Empty)));
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
        // committed regardless of whether Sphere's (currently stubbed) sync succeeds.
        await _sphereSyncClient.NotifyProfileClaimedAsync(entity.GitHubHandle, entity.SphereProfileId, cancellationToken);

        return Ok();
    }
}

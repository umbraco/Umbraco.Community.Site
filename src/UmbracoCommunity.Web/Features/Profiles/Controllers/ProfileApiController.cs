using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Profile-editing API behind the Account page (avatar, name, headline, bio, visibility) —
/// always scoped to the current signed-in member, never trusting an id from the caller.
/// Every POST, PUT and DELETE must carry the antiforgery request token from
/// <c>api/antiforgery/token</c> in the <c>RequestVerificationToken</c> header.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/profile")]
public class ProfileApiController : ControllerBase
{
    private readonly IMemberManager _memberManager;
    private readonly MemberProfileStore _store;
    private readonly ProfileAvatarUrlResolver _avatarUrlResolver;
    private readonly AvatarUploadService _avatarUploadService;

    public ProfileApiController(
        IMemberManager memberManager,
        MemberProfileStore store,
        ProfileAvatarUrlResolver avatarUrlResolver,
        AvatarUploadService avatarUploadService)
    {
        _memberManager = memberManager;
        _store = store;
        _avatarUrlResolver = avatarUrlResolver;
        _avatarUploadService = avatarUploadService;
    }

    /// <summary>Sets the member's own bio. Empty or whitespace-only text clears it, so the GitHub bio shows again.</summary>
    [HttpPut("bio")]
    public async Task<IActionResult> UpdateBio([FromBody] UpdateBioRequest request, CancellationToken cancellationToken)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (ProfileText.Normalize(request.Bio) is { Length: > ProfileText.MaxBioLength })
        {
            return BadRequest(new { error = $"Bio must be {ProfileText.MaxBioLength} characters or fewer." });
        }

        var entity = await _store.UpdateBioAsync(member.Key, request.Bio, cancellationToken);
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
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (ProfileText.Normalize(request.Headline) is { Length: > ProfileText.MaxHeadlineLength })
        {
            return BadRequest(new { error = $"Headline must be {ProfileText.MaxHeadlineLength} characters or fewer." });
        }

        var entity = await _store.UpdateHeadlineAsync(member.Key, request.Headline, cancellationToken);
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
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        if (ProfileText.Normalize(request.DisplayName) is { Length: > ProfileText.MaxDisplayNameLength })
        {
            return BadRequest(new { error = $"Display name must be {ProfileText.MaxDisplayNameLength} characters or fewer." });
        }

        var entity = await _store.UpdateDisplayNameAsync(member.Key, request.DisplayName, cancellationToken);
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
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Unauthorized();
        }

        var updated = await _store.SetProfileVisibleAsync(member.Key, request.Visible, cancellationToken);
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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// Lets the signed-in member add activity to their own profile by hand, edit it and withdraw it again (see
/// <see cref="ProfileActivityEntry"/>). An entry shows on the profile as soon as it is added; a moderator can remove it
/// afterwards. Always scoped to the member <see cref="ProfileEditSubjectResolver"/> resolves (the signed-in member, or the member a
/// profile admin edits), never trusting an id from the caller, and only for members who are
/// offered it (see <see cref="ProfileAccessService.CanAddActivityAsync"/>). Every POST, PUT and DELETE must carry the
/// antiforgery request token from <c>api/antiforgery/token</c> in the <c>RequestVerificationToken</c> header. An entry is
/// added and edited as <c>multipart/form-data</c> (see <see cref="AddActivityEntryForm"/>) so it can carry a picture.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[ProfileEditAudit]
[Route("api/profile/activity")]
public class ProfileActivityEntryApiController : ControllerBase
{
    private const string NotOfferedError = "Adding activity to your profile isn't available to you yet.";
    private const string NoProfileError = "Finish setting up your profile first.";

    private readonly ProfileEditSubjectResolver _subjects;
    private readonly MemberProfileStore _profileStore;
    private readonly ProfileAccessService _profileAccessService;
    private readonly ProfileActivityEntryService _activityEntryService;
    private readonly ProfileActivityImageStore _imageStore;
    private readonly ProfileActivityLinkLookupService _linkLookupService;

    public ProfileActivityEntryApiController(
        ProfileEditSubjectResolver subjects,
        MemberProfileStore profileStore,
        ProfileAccessService profileAccessService,
        ProfileActivityEntryService activityEntryService,
        ProfileActivityImageStore imageStore,
        ProfileActivityLinkLookupService linkLookupService)
    {
        _linkLookupService = linkLookupService;
        _subjects = subjects;
        _profileStore = profileStore;
        _profileAccessService = profileAccessService;
        _activityEntryService = activityEntryService;
        _imageStore = imageStore;
    }

    /// <summary>The member's entries that are shown, (legacy) waiting or removed by a moderator, newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        var entries = await _activityEntryService.ListForMemberAsync(profile.Id, cancellationToken);
        var pagePictures = await _linkLookupService.StoredPagePicturesAsync(
            entries.Where(e => e.ImagePath == null).Select(e => e.Url),
            cancellationToken);
        return Ok(entries.Select(e => ToResponse(e, pagePictures.GetValueOrDefault(e.Url))).ToList());
    }

    /// <summary>
    /// Adds an entry, shown on the profile at once, with the picture the member attached (optional; see
    /// <see cref="ProfileActivityImageStore"/>), which its timeline card then shows.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileActivityEntry.MaxAddRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileActivityEntry.MaxAddRequestBytes)]
    public async Task<IActionResult> Add([FromForm] AddActivityEntryForm form, CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        if (!ProfileActivityEntry.TryNormalize(form.ToRequest(), DateOnly.FromDateTime(DateTime.UtcNow), out var entry, out var error))
        {
            return BadRequest(new { error });
        }

        var (imagePath, imageError) = await SaveImageAsync(form.Image, cancellationToken);
        if (imageError != null)
        {
            return BadRequest(new { error = imageError });
        }

        // Deletes the picture again when the entry can't be added.
        return await ToResultAsync(await _activityEntryService.AddAsync(profile.Id, entry, imagePath, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// What the page behind a link offers for a new entry (title, kind, date, description, picture), so the form can be
    /// filled in (see <see cref="ProfileActivityLinkLookupService"/>). Always 200 for a member who may add activity:
    /// a link that can't be read, isn't https, or one more than the member's page reads allow, gives an empty answer
    /// and the member just types.
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] LookUpActivityLinkRequest request, CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        return Ok(await _linkLookupService.LookUpAsync(profile.MemberKey, request?.Url, cancellationToken));
    }

    /// <summary>
    /// Edits one of the member's own entries, with the same rules as adding. The entry keeps its id (so pins and hidden
    /// choices stay) and its state. A new <c>image</c> replaces the picture; without one, <c>removeImage=true</c> removes
    /// it; otherwise it is kept. An entry a moderator removed can't be edited (409); it can be withdrawn.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileActivityEntry.MaxAddRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileActivityEntry.MaxAddRequestBytes)]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateActivityEntryForm form, CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        if (!ProfileActivityEntry.TryNormalize(form.ToRequest(), DateOnly.FromDateTime(DateTime.UtcNow), out var entry, out var error))
        {
            return BadRequest(new { error });
        }

        var (imagePath, imageError) = await SaveImageAsync(form.Image, cancellationToken);
        if (imageError != null)
        {
            return BadRequest(new { error = imageError });
        }

        var image = imagePath != null
            ? ActivityEntryImageChange.Replace(imagePath)
            : form.RemoveImage ? ActivityEntryImageChange.RemoveImage : ActivityEntryImageChange.Keep;

        // Deletes the new picture again when the edit isn't saved, and the old one once it is.
        return await ToResultAsync(await _activityEntryService.UpdateAsync(profile.Id, id, entry, image, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Withdraws one of the member's own entries; one that was shown stops showing at once. An entry a moderator removed
    /// can be withdrawn too, which deletes its picture and takes it off the member's list (its link still can't be added
    /// again; see <see cref="MemberActivityEntryStore.WithdrawAsync"/>).
    /// </summary>
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
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return (null, resolved.FailureResult()!);
        }

        if (!await _profileAccessService.CanAddActivityAsync(subject))
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden, new { error = NotOfferedError }));
        }

        var profile = await _profileStore.GetByMemberKeyAsync(subject.MemberKey, cancellationToken);
        return profile?.OnboardingStatus == OnboardingStatus.Completed
            ? (profile, null)
            : (null, BadRequest(new { error = NoProfileError }));
    }

    /// <summary>Stores the attached picture, if any. Returns its path, or the message for the member when it isn't accepted.</summary>
    private async Task<(string? Path, string? Error)> SaveImageAsync(IFormFile? image, CancellationToken cancellationToken)
    {
        if (image == null)
        {
            return (null, null);
        }

        await using var stream = image.OpenReadStream();
        return await _imageStore.SaveAsync(stream, image.Length, cancellationToken);
    }

    private async Task<IActionResult> ToResultAsync(ActivityEntryChange change, CancellationToken cancellationToken)
    {
        if (change.Saved)
        {
            var entry = change.Entry!;
            var pagePictures = entry.ImagePath == null
                ? await _linkLookupService.StoredPagePicturesAsync([entry.Url], cancellationToken)
                : null;
            return Ok(ToResponse(entry, pagePictures?.GetValueOrDefault(entry.Url)));
        }

        var error = ProfileActivityEntry.ChangeError(change.Outcome);
        return change.Outcome == ActivityEntryChangeOutcome.NotFound
            ? NotFound(new { error })
            : Conflict(new { error });
    }

    /// <param name="entry">The entry.</param>
    /// <param name="pageImageUrl">Our copy of the linked page's picture, which the entry's card shows when the member attached none.</param>
    private MemberActivityEntryResponse ToResponse(ActivityEntryRecord entry, string? pageImageUrl) =>
        new(
            entry.Id,
            entry.Kind,
            ProfileActivityEntry.KindLabel(entry.Kind),
            entry.Title,
            entry.Url,
            entry.Date,
            entry.Description,
            entry.State.ToString(),
            entry.RejectionReason,
            _activityEntryService.ImageUrl(entry),
            pageImageUrl);
}

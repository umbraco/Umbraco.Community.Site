using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Controllers;

/// <summary>
/// The links found for the signed-in member, and their way to say a source is not theirs (see
/// <see cref="ProfileLinkMatchingService"/>). Always scoped to the member <see cref="ProfileEditSubjectResolver"/> resolves (the signed-in member, or the member a
/// profile admin edits), never trusting an id from the caller,
/// and only for members whose links are checked (see <see cref="ProfileAccessService.CanMatchLinksAsync"/>). Every POST
/// must carry the antiforgery request token from <c>api/antiforgery/token</c> in the <c>RequestVerificationToken</c> header.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[ProfileEditAudit]
[Route("api/profile/links")]
public class ProfileLinkMatchingApiController : ControllerBase
{
    private const string NotOfferedError = "Finding links for your profile isn't available to you yet.";
    private const string NoProfileError = "Finish setting up your profile first.";
    private const string InvalidUrlError = "Please provide a valid https URL.";
    private const string CannotRejectError = "That link can't be marked as not yours.";

    private readonly ProfileEditSubjectResolver _subjects;
    private readonly MemberProfileStore _profileStore;
    private readonly ProfileAccessService _profileAccessService;
    private readonly ProfileLinkMatchingService _linkMatching;

    public ProfileLinkMatchingApiController(
        ProfileEditSubjectResolver subjects,
        MemberProfileStore profileStore,
        ProfileAccessService profileAccessService,
        ProfileLinkMatchingService linkMatching)
    {
        _subjects = subjects;
        _profileStore = profileStore;
        _profileAccessService = profileAccessService;
        _linkMatching = linkMatching;
    }

    /// <summary>Accounts and feeds that look like the member's and are not among their links yet.</summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> Suggestions(CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        var memberLinks = profile.Feeds.Where(f => !f.IsRemoved).Select(f => f.Url).ToList();
        return Ok(await _linkMatching.GetSuggestionsAsync(profile.GitHubHandle, memberLinks, cancellationToken));
    }

    /// <summary>
    /// The member says a source is not theirs: activity from it stops showing on their profile at once, and the
    /// decision shows in the match review dashboard.
    /// </summary>
    [HttpPost("not-me")]
    public async Task<IActionResult> NotMe([FromBody] RejectLinkRequest request, CancellationToken cancellationToken = default)
    {
        var (profile, failure) = await ResolveProfileAsync(cancellationToken);
        if (profile == null)
        {
            return failure!;
        }

        if (!ProfileLinkUrl.TryParse(request.Url, out _))
        {
            return BadRequest(new { error = InvalidUrlError });
        }

        return await _linkMatching.RejectSourceAsync(profile.Id, profile.GitHubHandle, request.Url!, cancellationToken)
            ? Ok()
            : BadRequest(new { error = CannotRejectError });
    }

    private async Task<(MemberProfileEntity? Profile, IActionResult? Failure)> ResolveProfileAsync(CancellationToken cancellationToken)
    {
        var resolved = await _subjects.ResolveForApiAsync(Request, cancellationToken);
        if (resolved.Subject is not { } subject)
        {
            return (null, resolved.FailureResult()!);
        }

        if (!await _profileAccessService.CanMatchLinksAsync(subject))
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden, new { error = NotOfferedError }));
        }

        var profile = await _profileStore.GetByMemberKeyAsync(subject.MemberKey, cancellationToken);
        return profile?.OnboardingStatus == OnboardingStatus.Completed
            ? (profile, null)
            : (null, BadRequest(new { error = NoProfileError }));
    }
}

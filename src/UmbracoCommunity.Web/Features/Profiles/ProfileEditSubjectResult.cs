using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>The member a request acts on, or why there is none.</summary>
public sealed record ProfileEditSubjectResult(ProfileEditSubject? Subject, ProfileEditSubjectFailure Failure)
{
    public const string NotAllowedError = "You can't edit someone else's profile.";
    public const string SelfOnlyError = "This can only be done on your own profile.";
    public const string NotFoundError = "There is no profile with that handle.";

    public static ProfileEditSubjectResult Of(ProfileEditSubject subject) => new(subject, ProfileEditSubjectFailure.None);

    public static ProfileEditSubjectResult Failed(ProfileEditSubjectFailure failure) => new(null, failure);

    /// <summary>The API response for a failure: 401, 403 or 404, with a message for the member. Null when there is a subject.</summary>
    public IActionResult? FailureResult(string notAllowedError = NotAllowedError) => Failure switch
    {
        ProfileEditSubjectFailure.None => null,
        ProfileEditSubjectFailure.NotSignedIn => new UnauthorizedResult(),
        ProfileEditSubjectFailure.NotAllowed => new ObjectResult(new { error = notAllowedError }) { StatusCode = StatusCodes.Status403Forbidden },
        _ => new NotFoundObjectResult(new { error = NotFoundError }),
    };
}

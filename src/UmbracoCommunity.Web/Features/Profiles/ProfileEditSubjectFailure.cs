namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>Why a request has no profile to act on (see <see cref="ProfileEditSubjectResolver"/>).</summary>
public enum ProfileEditSubjectFailure
{
    None,

    /// <summary>Nobody is signed in: 401.</summary>
    NotSignedIn,

    /// <summary>Another member was named, and the signed-in member isn't a profile admin (or the action is self-only): 403.</summary>
    NotAllowed,

    /// <summary>Another member was named and there is no such member profile: 404.</summary>
    NotFound,
}

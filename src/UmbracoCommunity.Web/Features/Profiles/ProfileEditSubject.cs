using Umbraco.Cms.Core.Security;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Whose profile a request acts on (see <see cref="ProfileEditSubjectResolver"/>): the signed-in member themselves or, on
/// a profile admin's edit-as request, the member whose profile row they are editing. That member needs no Umbraco
/// member record: the profile row (key and handle) is enough.
/// </summary>
/// <param name="MemberKey">The key of the member whose profile is read and changed.</param>
/// <param name="Handle">Their GitHub handle.</param>
/// <param name="SignedInMember">The member making the request.</param>
public sealed record ProfileEditSubject(Guid MemberKey, string Handle, MemberIdentityUser SignedInMember)
{
    /// <summary>True when a profile admin acts on someone else's profile.</summary>
    public bool IsEditAs => MemberKey != SignedInMember.Key;

    /// <summary>The signed-in member's own profile.</summary>
    public static ProfileEditSubject Self(MemberIdentityUser member) => new(member.Key, member.UserName ?? string.Empty, member);
}

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles.Data;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The one place that decides whose profile a request to the account page or a member profile API acts on.
/// <list type="bullet">
/// <item>By default, the signed-in member (401 when nobody is signed in).</item>
/// <item>When the request names another member (<see cref="QueryKey"/> on the account page, the <see cref="HeaderName"/>
/// header on the APIs): the signed-in member must be in the <see cref="ProfileAccessService.DevRelGroupName"/>
/// member group (else <see cref="ProfileEditSubjectFailure.NotAllowed"/>), and the handle must have a profile row
/// (else <see cref="ProfileEditSubjectFailure.NotFound"/>); an Umbraco member behind it is not needed, but one with that
/// username and another key makes it not found. A named member never falls back to the signed-in
/// member's own profile.</item>
/// </list>
/// Nothing is remembered between requests: every request names its member again. Scoped; the last resolved subject is
/// kept for the audit line (see <see cref="ProfileEditAuditAttribute"/>).
/// </summary>
public sealed class ProfileEditSubjectResolver
{
    public const string HeaderName = "X-Profile-Edit-As";
    public const string QueryKey = "edit-as";

    private readonly IMemberManager _memberManager;
    private readonly ProfileAccessService _profileAccess;
    private readonly MemberProfileStore _profileStore;
    private readonly ILogger<ProfileEditSubjectResolver> _logger;

    public ProfileEditSubjectResolver(
        IMemberManager memberManager,
        ProfileAccessService profileAccess,
        MemberProfileStore profileStore,
        ILogger<ProfileEditSubjectResolver> logger)
    {
        _memberManager = memberManager;
        _profileAccess = profileAccess;
        _profileStore = profileStore;
        _logger = logger;
    }

    /// <summary>The subject this request resolved to last, for the audit line.</summary>
    public ProfileEditSubject? Resolved { get; private set; }

    /// <summary>The handle an API request names in <see cref="HeaderName"/>, or null when it names none.</summary>
    public static string? RequestedHandle(HttpRequest? request) =>
        request != null && request.Headers.TryGetValue(HeaderName, out var values) ? values.ToString().Trim() : null;

    /// <summary>For a member profile API: the member named in <see cref="HeaderName"/>, or the signed-in member.</summary>
    public Task<ProfileEditSubjectResult> ResolveForApiAsync(HttpRequest? request, CancellationToken cancellationToken = default) =>
        ResolveAsync(RequestedHandle(request), cancellationToken);

    /// <summary>
    /// For a self-only action: the signed-in member. A request naming anyone else is refused
    /// (<see cref="ProfileEditSubjectFailure.NotAllowed"/>), whoever makes it.
    /// </summary>
    public async Task<ProfileEditSubjectResult> ResolveSelfOnlyAsync(HttpRequest? request)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return ProfileEditSubjectResult.Failed(ProfileEditSubjectFailure.NotSignedIn);
        }

        var requested = RequestedHandle(request);
        if (requested != null && !string.Equals(requested, member.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return ProfileEditSubjectResult.Failed(ProfileEditSubjectFailure.NotAllowed);
        }

        return ProfileEditSubjectResult.Of(Remember(ProfileEditSubject.Self(member)));
    }

    /// <summary>
    /// The member a request acts on. <paramref name="requestedHandle"/> null: the signed-in member. Anything else
    /// (even empty) names a member: themselves, or someone else for a profile admin only.
    /// </summary>
    public async Task<ProfileEditSubjectResult> ResolveAsync(string? requestedHandle, CancellationToken cancellationToken = default)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return ProfileEditSubjectResult.Failed(ProfileEditSubjectFailure.NotSignedIn);
        }

        if (requestedHandle == null || string.Equals(requestedHandle, member.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return ProfileEditSubjectResult.Of(Remember(ProfileEditSubject.Self(member)));
        }

        if (!await _profileAccess.IsCurrentMemberProfileAdminAsync())
        {
            _logger.LogWarning("Member {MemberKey} asked to edit another member's profile without being a profile admin", member.Key);
            return ProfileEditSubjectResult.Failed(ProfileEditSubjectFailure.NotAllowed);
        }

        var target = await FindProfileAsync(requestedHandle, cancellationToken);
        return target == null
            ? ProfileEditSubjectResult.Failed(ProfileEditSubjectFailure.NotFound)
            : ProfileEditSubjectResult.Of(Remember(new ProfileEditSubject(target.Value.MemberKey, target.Value.Handle, member)));
    }

    /// <summary>Writes the audit line for a successful change on someone else's profile. Keys only, no profile text.</summary>
    public void LogEditAs(ProfileEditSubject subject, string action)
    {
        if (subject.IsEditAs)
        {
            _logger.LogInformation(
                "Profile admin {AdminMemberKey} changed the profile of member {MemberKey}: {Action}",
                subject.SignedInMember.Key,
                subject.MemberKey,
                action);
        }
    }

    // The profile row for a handle is enough: a profile need not have an Umbraco member behind it. When a member with
    // that username does exist, it must be the profile's own member (same key), or the handle counts as unknown.
    private async Task<(Guid MemberKey, string Handle)?> FindProfileAsync(string handle, CancellationToken cancellationToken)
    {
        if (handle.Length == 0)
        {
            return null;
        }

        var profile = await _profileStore.GetByHandleAsync(handle, cancellationToken);
        if (profile == null)
        {
            return null;
        }

        var member = await _memberManager.FindByNameAsync(profile.GitHubHandle);
        return member == null || member.Key == profile.MemberKey ? (profile.MemberKey, profile.GitHubHandle) : null;
    }

    private ProfileEditSubject Remember(ProfileEditSubject subject)
    {
        Resolved = subject;
        return subject;
    }
}

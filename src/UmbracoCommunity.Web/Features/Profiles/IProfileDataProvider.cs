using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Supplies a <see cref="CommunityProfile"/> for a given slug (the member's GitHub
/// handle). Phase 1 is backed by an in-repo fixture (<see cref="DummyProfileDataProvider"/>);
/// a later phase swaps in an external-platform-backed implementation with no change to callers.
/// </summary>
public interface IProfileDataProvider
{
    /// <summary>
    /// Returns the profile for the given <paramref name="slug"/>, or <c>null</c> if no
    /// such profile exists (the caller renders a 404).
    /// </summary>
    /// <param name="slug">The member's GitHub handle, taken from the request URL.</param>
    /// <param name="includeHidden">
    /// Also return a profile its owner has not shown yet. Only pass <c>true</c> when the
    /// signed-in member is the owner of <paramref name="slug"/> or in the DevRel member group
    /// (see <see cref="ProfileAccessService"/>).
    /// </param>
    /// <param name="includeHiddenLinks">
    /// Also return the links the member hid, marked with <see cref="ProfileSocialLink.IsHiddenByMember"/>. Only pass
    /// <c>true</c> for a DevRel view of someone else's profile (see <see cref="ProfileAccessService.IsDevRelViewAsync"/>).
    /// </param>
    Task<CommunityProfile?> GetProfileAsync(
        string slug,
        bool includeHidden = false,
        bool includeHiddenLinks = false,
        CancellationToken cancellationToken = default);
}

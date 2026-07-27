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
    Task<CommunityProfile?> GetProfileAsync(string slug, CancellationToken cancellationToken = default);
}

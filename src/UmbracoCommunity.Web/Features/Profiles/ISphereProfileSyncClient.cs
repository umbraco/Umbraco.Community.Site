namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Notifies Sphere of profile-claim and feed changes. Sphere has no profile/feed endpoints
/// today — <see cref="StubSphereProfileSyncClient"/> is a safe no-op implementation for now,
/// mirroring the <see cref="IProfileDataProvider"/>/<see cref="DummyProfileDataProvider"/>
/// seam: swap in a real <c>SphereApiClient</c>-based implementation once those endpoints
/// exist, with no change to callers.
/// </summary>
public interface ISphereProfileSyncClient
{
    Task NotifyProfileClaimedAsync(string handle, string? sphereProfileId, CancellationToken cancellationToken = default);

    Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default);

    /// <summary>Hidden ≠ removed — a hidden feed is reported as "unlisted", not deleted.</summary>
    Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default);

    Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default);
}

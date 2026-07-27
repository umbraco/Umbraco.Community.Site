namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Notifies the external content platform of profile-claim and feed changes. That platform has
/// no profile/feed endpoints today — <see cref="StubProfileSyncClient"/> is a safe no-op
/// implementation for now, mirroring the <see cref="IProfileDataProvider"/>/<see cref="DummyProfileDataProvider"/>
/// seam: swap in a real <c>CommunityBlogsApiClient</c>-based implementation once those endpoints
/// exist, with no change to callers.
/// </summary>
public interface IProfileSyncClient
{
    Task NotifyProfileClaimedAsync(string handle, string? platformProfileId, CancellationToken cancellationToken = default);

    Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default);

    /// <summary>Hidden ≠ removed — a hidden feed is reported as "unlisted", not deleted.</summary>
    Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default);

    Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the external platform last synced this member's profile data (blog posts, talks, badges, etc.).
    /// Returns <c>null</c> if the member has never been synced.
    /// </summary>
    Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default);
}

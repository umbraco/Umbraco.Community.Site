using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Talks to the external content platform about a profile. Reads a member's public activity from it
/// (<see cref="FindProfileAsync"/>, <see cref="GetActivitiesAsync"/>); the platform has no endpoints
/// yet for profile-claim, feed and activity notifications, so <see cref="PlatformProfileSyncClient"/> only logs those.
/// </summary>
public interface IProfileSyncClient
{
    /// <summary>
    /// Looks the member up on the platform by GitHub handle. Returns <c>null</c> when the platform has no
    /// profile for the handle or is not configured. Throws when the platform cannot be reached or errors.
    /// </summary>
    Task<PlatformProfile?> FindProfileAsync(string handle, CancellationToken cancellationToken = default);

    /// <summary>
    /// The newest activity of one <paramref name="group"/> (see <see cref="PlatformActivityGroups"/>) for a
    /// platform profile, newest first. Throws when the platform cannot be reached or errors.
    /// </summary>
    Task<IReadOnlyList<PlatformActivity>> GetActivitiesAsync(string platformProfileId, string group, int limit, CancellationToken cancellationToken = default);

    Task NotifyProfileClaimedAsync(string handle, string? platformProfileId, CancellationToken cancellationToken = default);

    Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default);

    /// <summary>Hidden ≠ removed — a hidden feed is reported as "unlisted", not deleted.</summary>
    Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default);

    Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default);

    /// <summary>A member's hand-added activity was verified and now shows on their profile.</summary>
    Task NotifyActivityVerifiedAsync(string handle, ActivityEntryRecord entry, CancellationToken cancellationToken = default);

    /// <summary>Activity that was verified is no longer shown on the profile.</summary>
    Task NotifyActivityRemovedAsync(string handle, string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the external platform last synced this member's profile data (blog posts, talks, badges, etc.).
    /// Returns <c>null</c> if the member has never been synced.
    /// </summary>
    Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default);
}

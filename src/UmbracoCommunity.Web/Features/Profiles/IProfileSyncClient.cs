using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Talks to the external content platform about a profile. Reads a member's public activity from it
/// (<see cref="FindProfileAsync"/>, <see cref="GetActivitiesAsync"/>) and asks it about a member's links
/// (<see cref="VerifyLinksAsync"/>); the platform has no endpoints
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

    /// <summary>
    /// Every activity of one <paramref name="group"/> from <paramref name="from"/> on, newest first, following the
    /// platform's pages until the end or <paramref name="maxItems"/>. Used to count a window exactly, not to show items.
    /// Throws when the platform cannot be reached or errors.
    /// </summary>
    Task<IReadOnlyList<PlatformActivity>> GetActivitiesSinceAsync(string platformProfileId, string group, DateOnly from, int maxItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// The exact forum counts for a platform profile over the last <paramref name="months"/> months (1 to 24), or null
    /// when the platform is not configured. Throws when the platform cannot be reached or errors.
    /// </summary>
    Task<PlatformForumStats?> GetForumStatsAsync(string platformProfileId, int months, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks whether the platform already ties each of <paramref name="urls"/> to the profile of the GitHub user
    /// <paramref name="handle"/>. Only https addresses are sent. Returns an empty list when the platform is not
    /// configured. Throws when the platform cannot be reached or errors.
    /// </summary>
    Task<IReadOnlyList<PlatformLinkCheck>> VerifyLinksAsync(string handle, IReadOnlyList<string> urls, CancellationToken cancellationToken = default);

    Task NotifyProfileClaimedAsync(string handle, string? platformProfileId, CancellationToken cancellationToken = default);

    Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default);

    /// <summary>Hidden ≠ removed — a hidden feed is reported as "unlisted", not deleted.</summary>
    Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default);

    Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// A member's hand-added activity shows on their profile: it was just added, a moderator showed or restored it, or the
    /// member edited it (sent again with the new title, description and date). Treat it as add-or-update keyed by the
    /// entry's url; when an edit changes the url, <see cref="NotifyActivityRemovedAsync"/> is sent for the old one first.
    /// </summary>
    Task NotifyActivityVerifiedAsync(string handle, ActivityEntryRecord entry, CancellationToken cancellationToken = default);

    /// <summary>Hand-added activity no longer shows on the profile at <paramref name="url"/>: withdrawn, removed by a moderator, or its link edited.</summary>
    Task NotifyActivityRemovedAsync(string handle, string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the external platform last synced this member's profile data (blog posts, talks, badges, etc.).
    /// Returns <c>null</c> if the member has never been synced.
    /// </summary>
    Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default);
}

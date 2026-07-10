namespace UmbracoCommunity.BlogAnnouncements;

/// <summary>
/// Host-provided hook that runs one full community-feed poll cycle on demand (fetch fresh posts,
/// refresh the host's caches, and run announcement detection/delivery) — the same path the host's
/// periodic timer executes. Registration is optional: when the host doesn't provide an
/// implementation, the dashboard's "Poll now" action reports itself unavailable instead of failing.
/// Kept in this project so the dashboard needs no reference to the host's feed services.
/// </summary>
public interface ICommunityFeedPoller
{
    /// <summary>Runs one complete poll cycle to completion.</summary>
    Task PollNowAsync(CancellationToken cancellationToken);
}

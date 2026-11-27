namespace UmbracoCommunity.BlogAnnouncements.Monitoring;

/// <summary>
/// One "the pipeline has gone quiet" alert: when the last cycle ran, and how long ago that is.
/// </summary>
public sealed record StaleRunAlert(DateTime LastRunUtc, TimeSpan Age, DateTime NowUtc);

/// <summary>
/// Delivers a <see cref="StaleRunAlert"/> somewhere a human will see it. Separate from
/// <c>IDiscordAnnouncer</c> on purpose: announcements go to the public community channel, these go
/// to an operations channel, and the two must never share a webhook.
/// </summary>
public interface IHeartbeatAlerter
{
    /// <summary>Returns true when the alert was delivered.</summary>
    Task<bool> AlertAsync(StaleRunAlert alert, CancellationToken cancellationToken);
}

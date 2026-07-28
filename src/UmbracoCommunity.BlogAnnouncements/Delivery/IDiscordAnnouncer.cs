namespace UmbracoCommunity.BlogAnnouncements.Delivery;

/// <summary>
/// The delivery leg of the announcement pipeline. Kept behind an interface so an Umbraco Automate
/// trigger can replace the direct webhook call later without touching detection. Manual dashboard
/// actions (repost / post-now) must call the same implementation so formatting never drifts.
/// </summary>
public interface IDiscordAnnouncer
{
    Task<DeliveryResult> AnnounceAsync(AnnouncementPayload payload, CancellationToken cancellationToken);
}

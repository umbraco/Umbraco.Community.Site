namespace UmbracoCommunity.BlogAnnouncements;

/// <summary>
/// Configuration for the Discord blog-announcement pipeline, bound to the nested
/// "CommunityBlogs:Announcements" section — the announcement settings live under the host's
/// community-blogs config, next to its other keys (e.g. <c>CommunityBlogs:ApiKey</c>). The Discord
/// webhook URL is a secret — supply it via appsettings.Local.json / env
/// (<c>CommunityBlogs:Announcements:Discord:WebhookUrl</c>), never the committed appsettings.
/// </summary>
public sealed class BlogAnnouncementsOptions
{
    public const string SectionName = "CommunityBlogs:Announcements";

    /// <summary>
    /// Posts first seen and published within this many days are eligible to announce. Anything
    /// older is recorded as <c>SkippedTooOld</c> silently, killing the repost-flood failure mode.
    /// </summary>
    public int RecencyWindowDays { get; set; } = 7;

    /// <summary>
    /// Maximum posts announced per detection cycle. Excess eligible posts stay <c>Pending</c> and
    /// drain on subsequent cycles — a guardrail against back-catalogue floods.
    /// </summary>
    public int MaxAnnouncementsPerCycle { get; set; } = 5;

    /// <summary>
    /// When true (the default), the pipeline logs the would-be Discord payload and posts nothing;
    /// rows stay <c>Pending</c> and attempts are recorded with outcome <c>DryRun</c>. Flip to false
    /// only at cutover.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Absolute origin of the public site (e.g. <c>https://community.umbraco.com</c>), used to
    /// resolve root-relative image URLs (community blog cover images/avatars are localized to
    /// <c>/community-blog-images/{file}</c> under the host's wwwroot — see
    /// <c>CommunityBlogsImageDownloader</c>) into absolute URLs before they're sent to Discord.
    /// Browsers resolve root-relative URLs against the page they're on; Discord's embed validator
    /// requires a fully-qualified URL and returns 400 otherwise. Empty by default — a relative
    /// image/avatar URL is dropped from the embed rather than sent broken.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>Discord delivery configuration.</summary>
    public DiscordAnnouncementOptions Discord { get; set; } = new();

    /// <summary>Stale-pipeline alerting. Off by default — see <see cref="MonitoringOptions"/>.</summary>
    public MonitoringOptions Monitoring { get; set; } = new();
}

/// <summary>
/// Watches for the pipeline going silent. The failure this exists for is an *absence*: when the
/// Automate engine fails to start, no run records are written, no automation run fails, and
/// nothing surfaces anywhere — the pipeline simply stops, as it did for five days in November 2026.
/// </summary>
public sealed class MonitoringOptions
{
    /// <summary>
    /// Off by default so local and test environments stay quiet; switch on where a webhook is set.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How long without a completed cycle before the pipeline counts as stale. The pipeline runs
    /// every 15 minutes, so this tolerates several missed ticks before crying wolf.
    /// </summary>
    public int StaleAfterMinutes { get; set; } = 90;

    /// <summary>
    /// How long to wait before repeating an alert for an outage that is still going, so a long
    /// outage is a reminder every few hours rather than a message every check.
    /// </summary>
    public int RepeatAfterHours { get; set; } = 6;

    /// <summary>
    /// Discord webhook for alerts. Secret — set as
    /// <c>CommunityBlogs:Announcements:Monitoring:Discord:WebhookUrl</c>. This must be an
    /// operations channel, never the public channel the announcements themselves go to.
    /// </summary>
    public DiscordAnnouncementOptions Discord { get; set; } = new();
}

public sealed class DiscordAnnouncementOptions
{
    /// <summary>
    /// Discord incoming-webhook URL. Secret — set in appsettings.Local.json / env as
    /// <c>CommunityBlogs:Announcements:Discord:WebhookUrl</c>. Empty by default; when empty and
    /// dry-run is off, delivery fails (and retries) rather than posting.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;
}

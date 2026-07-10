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

    /// <summary>Discord delivery configuration.</summary>
    public DiscordAnnouncementOptions Discord { get; set; } = new();

    /// <summary>
    /// How often the host polls for new posts, in minutes — and therefore how often announcements
    /// can go out. NOT a key of the Announcements section: the composer populates it from the
    /// host's <c>CommunityBlogs:RefreshIntervalInMinutes</c> (default 15), keeping this project
    /// decoupled from the host's own options types. Display-only; the host owns the timer (which
    /// floors the effective interval at 5 minutes).
    /// </summary>
    public int PollIntervalMinutes { get; set; } = 15;
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

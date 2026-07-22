namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public class CommunityBlogsOptions
{
    public const string SectionName = "CommunityBlogs";

    /// <summary>
    /// Base URL of the external content aggregation API (must end with a trailing slash). No
    /// default — must be supplied via <c>appsettings.Local.json</c> or environment config in every
    /// environment; deliberately not hardcoded here.
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>API key sent in the <c>Authorization</c> header (bare key, no "Bearer"). Supplied via appsettings.Local.json / env — never committed.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// How often the background service re-aggregates posts, in minutes (floored at 5). This
    /// interval also paces new-post announcement detection, the blog-posts cache refresh, and the
    /// image downloader pass — the upstream fetch is capped at ~60 posts, so short intervals are cheap.
    /// </summary>
    public int RefreshIntervalInMinutes { get; set; } = 15;

    /// <summary>Per-request HTTP timeout.</summary>
    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// How many posts to request from the external API per cursor call (its "limit", max 100).
    /// This is the fetch batch size, NOT the number of posts shown on a page — that is the
    /// block's PostsPerPage setting.
    /// </summary>
    public int FetchBatchSize { get; set; } = 100;

    /// <summary>Maximum number of (newest) posts to keep, so pagination stays manageable (default 5 pages of 12).</summary>
    public int MaxPosts { get; set; } = 60;
}

namespace UmbracoCommunity.Web.Configuration;

/// <summary>
/// Configuration options for API output caching.
/// </summary>
public class OutputCacheOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "OutputCache";

    /// <summary>
    /// Cache duration in seconds for content-driven APIs (blog posts, articles, etc.).
    /// This cache is invalidated via Umbraco notifications when content changes,
    /// so a long duration is appropriate. Default: 86400 seconds (24 hours).
    /// </summary>
    public int ContentDrivenDurationSeconds { get; set; } = 86400;

    /// <summary>
    /// Cache duration in seconds for external API integrations (Sessionize, etc.)
    /// where we don't control data changes. Uses time-based expiration.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int ExternalApiDurationSeconds { get; set; } = 300;
}

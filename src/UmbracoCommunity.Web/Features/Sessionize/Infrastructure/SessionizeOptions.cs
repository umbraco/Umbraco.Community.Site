namespace UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

public class SessionizeOptions
{
    public const string SectionName = "Sessionize";

    /// <summary>
    /// The Sessionize event ID (found in your Sessionize dashboard URL or API endpoints)
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Base URL for Sessionize API (defaults to public API)
    /// </summary>
    public string BaseUrl { get; set; } = "https://sessionize.com/api/v2";

    /// <summary>
    /// Cache duration in minutes for session data (default: 15 minutes)
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Returns true if the Sessionize integration is properly configured
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(EventId);

    /// <summary>
    /// Gets the full API URL for a specific view
    /// </summary>
    public string GetApiUrl(string view) => $"{BaseUrl.TrimEnd('/')}/{EventId}/view/{view}";
}

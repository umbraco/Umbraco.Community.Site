namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public class CalendarFeedOptions
{
    public const string SectionName = "CalendarFeed";

    /// <summary>
    /// URL of the JSON calendar feed.
    /// </summary>
    public string Url { get; set; } = "https://umbracalendar.com/api/v1/feed.json";

    /// <summary>
    /// Cache duration in minutes for feed data.
    /// </summary>
    public int CacheDurationInMinutes { get; set; } = 60;
}

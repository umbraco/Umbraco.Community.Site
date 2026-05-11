namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

/// <summary>
/// Marker type for the named HttpClient used by <see cref="CalendarFeedService"/>.
/// Configured in <c>RegisterFeeds</c>.
/// </summary>
public sealed class CalendarFeedHttpClient
{
    public HttpClient Client { get; }

    public CalendarFeedHttpClient(HttpClient client) => Client = client;
}

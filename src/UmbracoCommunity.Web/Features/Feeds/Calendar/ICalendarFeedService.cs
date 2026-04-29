namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public interface ICalendarFeedService
{
    /// <summary>
    /// Fetches the configured calendar feed, filters out past events, sorts ascending by StartsAt,
    /// and returns the result. Caches for the configured duration. On error, returns the last
    /// successful response from a 7-day stale fallback if available, else empty.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(CancellationToken cancellationToken = default);
}

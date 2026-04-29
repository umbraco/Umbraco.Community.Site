using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public interface ICalendarFeedService
{
    /// <summary>
    /// Fetches the feed for the given calendarFeed published content node, filters out past events,
    /// sorts ascending by StartsAt, and returns the result. Caches per node for the configured duration.
    /// On error, returns the last successful response from a 7-day stale fallback if available, else empty.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(
        IPublishedContent feedNode,
        CancellationToken cancellationToken = default);
}

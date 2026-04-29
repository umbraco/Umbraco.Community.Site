using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public sealed class CalendarFeedService : ICalendarFeedService
{
    private static readonly TimeSpan StaleFallbackDuration = TimeSpan.FromDays(7);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CalendarFeedService> _logger;
    private readonly TimeProvider _time;

    public CalendarFeedService(
        CalendarFeedHttpClient typedClient,
        IMemoryCache cache,
        ILogger<CalendarFeedService> logger,
        TimeProvider time)
    {
        _http = typedClient.Client;
        _cache = cache;
        _logger = logger;
        _time = time;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(
        IPublishedContent feedNode,
        CancellationToken cancellationToken = default)
    {
        var feedUrl = feedNode.GetProperty("feedUrl")?.GetValue() as string;
        var cacheMinutes = feedNode.GetProperty("cacheDurationMinutes")?.GetValue() as int? ?? 0;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            _logger.LogWarning("Calendar feed node {Key} has no feedUrl; returning empty.", feedNode.Key);
            return Array.Empty<CalendarEvent>();
        }

        var primaryKey = $"calendar-feed:{feedNode.Key}";
        var staleKey = $"calendar-feed:{feedNode.Key}:stale";

        if (_cache.TryGetValue(primaryKey, out IReadOnlyList<CalendarEvent>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var feed = await _http.GetFromJsonAsync<CalendarFeed>(
                feedUrl, CalendarFeedJsonOptions.Default, cancellationToken)
                ?? throw new InvalidOperationException("Feed deserialised to null.");

            var upcoming = ProjectAndSort(feed);

            var primaryDuration = TimeSpan.FromMinutes(Math.Max(1, cacheMinutes));
            _cache.Set(primaryKey, upcoming, primaryDuration);
            _cache.Set(staleKey, upcoming, new MemoryCacheEntryOptions
            {
                SlidingExpiration = StaleFallbackDuration,
            });

            return upcoming;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to fetch calendar feed for node {Key} ({Url}); attempting stale fallback.",
                feedNode.Key, feedUrl);

            if (_cache.TryGetValue(staleKey, out IReadOnlyList<CalendarEvent>? stale) && stale is not null)
            {
                return stale;
            }

            return Array.Empty<CalendarEvent>();
        }
    }

    private IReadOnlyList<CalendarEvent> ProjectAndSort(CalendarFeed feed)
    {
        var nowUtc = _time.GetUtcNow();
        return feed.Events
            .Where(e => e.EndsAt > nowUtc)
            .OrderBy(e => e.StartsAt)
            .ToArray();
    }
}

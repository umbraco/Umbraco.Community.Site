using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public sealed class CalendarFeedService : ICalendarFeedService
{
    private const string PrimaryCacheKey = "calendar-feed:primary";
    private const string StaleCacheKey = "calendar-feed:stale";
    private static readonly TimeSpan StaleFallbackDuration = TimeSpan.FromDays(7);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CalendarFeedService> _logger;
    private readonly TimeProvider _time;
    private readonly IOptionsMonitor<CalendarFeedOptions> _options;

    public CalendarFeedService(
        CalendarFeedHttpClient typedClient,
        IMemoryCache cache,
        ILogger<CalendarFeedService> logger,
        TimeProvider time,
        IOptionsMonitor<CalendarFeedOptions> options)
    {
        _http = typedClient.Client;
        _cache = cache;
        _logger = logger;
        _time = time;
        _options = options;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.Url))
        {
            _logger.LogWarning("CalendarFeedOptions.Url is not configured; returning empty.");
            return Array.Empty<CalendarEvent>();
        }

        if (_cache.TryGetValue(PrimaryCacheKey, out IReadOnlyList<CalendarEvent>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var feed = await _http.GetFromJsonAsync<CalendarFeed>(
                options.Url, CalendarFeedJsonOptions.Default, cancellationToken)
                ?? throw new InvalidOperationException("Feed deserialised to null.");

            var upcoming = ProjectAndSort(feed);

            var primaryDuration = TimeSpan.FromMinutes(Math.Max(1, options.CacheDurationInMinutes));
            _cache.Set(PrimaryCacheKey, upcoming, primaryDuration);
            _cache.Set(StaleCacheKey, upcoming, new MemoryCacheEntryOptions
            {
                SlidingExpiration = StaleFallbackDuration,
            });

            return upcoming;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to fetch calendar feed from {Url}; attempting stale fallback.", options.Url);

            if (_cache.TryGetValue(StaleCacheKey, out IReadOnlyList<CalendarEvent>? stale) && stale is not null)
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

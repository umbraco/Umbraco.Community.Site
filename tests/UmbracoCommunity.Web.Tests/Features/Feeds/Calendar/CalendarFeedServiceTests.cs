using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

public class CalendarFeedServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-29T10:00:00Z");

    private static string SampleJson(params (string id, string startsAt, string endsAt)[] events)
    {
        var items = string.Join(",", events.Select(e => $$"""
        {
          "id": "{{e.id}}",
          "url": "https://x/{{e.id}}",
          "title": "Event {{e.id}}",
          "summary": null,
          "publishedAt": "2026-01-01T00:00:00Z",
          "startsAt": "{{e.startsAt}}",
          "endsAt": "{{e.endsAt}}",
          "location": null,
          "organizer": null,
          "attendanceMode": "inPerson",
          "isHqOrganized": false,
          "isCancelled": false
        }
        """));
        return $$"""
        {
          "feed": { "title": "x", "sourceUrl": "https://x", "generatedAt": "2026-04-29T08:00:00Z" },
          "events": [{{items}}]
        }
        """;
    }

    private static (CalendarFeedService Service, StubHandler Handler, MemoryCache Cache) CreateService(
        StubHandler handler,
        TimeProvider? timeProvider = null,
        CalendarFeedOptions? options = null)
    {
        var http = new HttpClient(handler);
        var typed = new CalendarFeedHttpClient(http);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<CalendarFeedService>.Instance;
        var optionsMonitor = new TestOptionsMonitor<CalendarFeedOptions>(
            options ?? new CalendarFeedOptions { Url = "https://example.com/feed.json", CacheDurationInMinutes = 60 });
        var service = new CalendarFeedService(typed, cache, logger, timeProvider ?? new FixedTimeProvider(Now), optionsMonitor);
        return (service, handler, cache);
    }

    [Fact]
    public async Task Filters_out_cancelled_events()
    {
        const string json = """
        {
          "feed": { "title": "x", "sourceUrl": "https://x", "generatedAt": "2026-04-29T08:00:00Z" },
          "events": [
            {
              "id": "live", "url": "https://x/live", "title": "Live", "summary": null,
              "publishedAt": "2026-01-01T00:00:00Z",
              "startsAt": "2026-05-01T00:00:00Z", "endsAt": "2026-05-01T01:00:00Z",
              "location": null, "organizer": null,
              "attendanceMode": "inPerson", "isHqOrganized": false, "isCancelled": false
            },
            {
              "id": "dead", "url": "https://x/dead", "title": "Dead", "summary": null,
              "publishedAt": "2026-01-01T00:00:00Z",
              "startsAt": "2026-05-02T00:00:00Z", "endsAt": "2026-05-02T01:00:00Z",
              "location": null, "organizer": null,
              "attendanceMode": "inPerson", "isHqOrganized": false, "isCancelled": true
            }
          ]
        }
        """;
        var (service, _, _) = CreateService(StubHandler.Json(json));
        var result = await service.GetUpcomingEventsAsync();
        result.Select(e => e.Id).Should().Equal("live");
    }

    [Fact]
    public async Task Returns_events_sorted_ascending_by_startsAt()
    {
        var json = SampleJson(
            ("c", "2026-08-01T00:00:00Z", "2026-08-01T01:00:00Z"),
            ("a", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"),
            ("b", "2026-06-01T00:00:00Z", "2026-06-01T01:00:00Z"));

        var (service, _, _) = CreateService(StubHandler.Json(json));
        var result = await service.GetUpcomingEventsAsync();

        result.Select(e => e.Id).Should().Equal("a", "b", "c");
    }

    [Fact]
    public async Task Filters_out_events_that_have_already_ended()
    {
        var json = SampleJson(
            ("past",   "2026-04-29T08:00:00Z", "2026-04-29T09:59:59Z"),
            ("future", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"));

        var (service, _, _) = CreateService(StubHandler.Json(json));
        var result = await service.GetUpcomingEventsAsync();

        result.Select(e => e.Id).Should().Equal("future");
    }

    [Fact]
    public async Task Event_ending_exactly_now_is_filtered()
    {
        var json = SampleJson(
            ("ending-now", "2026-04-29T08:00:00Z", "2026-04-29T10:00:00Z"));

        var (service, _, _) = CreateService(StubHandler.Json(json));
        var result = await service.GetUpcomingEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Caches_response_for_configured_duration()
    {
        var json = SampleJson(("a", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"));
        var (service, handler, _) = CreateService(StubHandler.Json(json));

        await service.GetUpcomingEventsAsync();
        await service.GetUpcomingEventsAsync();

        handler.CallCount.Should().Be(1, "second request should be served from cache");
    }

    [Fact]
    public async Task Returns_empty_on_first_failure_with_no_stale_fallback()
    {
        var (service, _, _) = CreateService(StubHandler.Throws());
        var result = await service.GetUpcomingEventsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_stale_fallback_when_upstream_fails_after_a_success()
    {
        var goodJson = SampleJson(("good", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"));

        var responses = new Queue<Func<HttpResponseMessage>>();
        responses.Enqueue(() => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(goodJson, System.Text.Encoding.UTF8, "application/json") });
        responses.Enqueue(() => throw new HttpRequestException("down"));

        var handler = new StubHandler(_ => responses.Dequeue()());
        var (service, _, cache) = CreateService(handler,
            options: new CalendarFeedOptions { Url = "https://example.com/feed.json", CacheDurationInMinutes = 1 });

        var first = await service.GetUpcomingEventsAsync();
        first.Select(e => e.Id).Should().Equal("good");

        // IMemoryCache expiry isn't driven by TimeProvider; manually evict the primary entry to
        // simulate its expiry, leaving the stale fallback entry intact.
        cache.Remove("calendar-feed:primary");

        var second = await service.GetUpcomingEventsAsync();
        second.Select(e => e.Id).Should().Equal("good");
    }

    [Fact]
    public async Task Returns_empty_when_status_code_is_non_2xx_and_no_fallback()
    {
        var (service, _, _) = CreateService(StubHandler.Status(System.Net.HttpStatusCode.InternalServerError));
        var result = await service.GetUpcomingEventsAsync();
        result.Should().BeEmpty();
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public FixedTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; set; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

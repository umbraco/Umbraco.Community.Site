using System.Net;
using Microsoft.AspNetCore.Http;
using UmbracoCommunity.SearchTracker.Recording;

namespace UmbracoCommunity.SearchTracker.Tests;

public class SearchTermRecorderTests
{
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0 Safari/537.36";
    private static readonly Guid Site = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static (SearchTermRecorder Recorder, SearchEventChannel Channel) Create(Action<SearchTrackerOptions>? configure = null)
    {
        var options = TestOptions.Create(configure);
        var channel = new SearchEventChannel(options);
        return (new SearchTermRecorder(channel, options, new FixedTimeProvider(Now)), channel);
    }

    private static HttpContext Request(string? userAgent = BrowserUserAgent, string ip = "203.0.113.5")
    {
        var context = new DefaultHttpContext();
        if (userAgent is not null) context.Request.Headers.UserAgent = userAgent;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private static List<SearchEvent> Drain(SearchEventChannel channel)
    {
        var events = new List<SearchEvent>();
        while (channel.Reader.TryRead(out var evt)) events.Add(evt);
        return events;
    }

    [Fact]
    public void Queues_a_normalised_search()
    {
        var (recorder, channel) = Create();

        recorder.Record(Site, "  Block Grid ", 0, Request());

        Drain(channel).Should().ContainSingle()
            .Which.Should().Be(new SearchEvent(Site, "block grid", 0, Now.UtcDateTime));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Googlebot/2.1 (+http://www.google.com/bot.html)")]
    [InlineData("python-requests/2.31")]
    [InlineData("Slackbot-LinkExpanding 1.0")]
    public void Skips_bots_and_missing_user_agents(string? userAgent)
    {
        var (recorder, channel) = Create();

        recorder.Record(Site, "forms", 3, Request(userAgent));

        Drain(channel).Should().BeEmpty();
    }

    [Fact]
    public void Skips_a_repeat_search_from_the_same_client()
    {
        var (recorder, channel) = Create();

        recorder.Record(Site, "Forms", 3, Request());
        recorder.Record(Site, "forms ", 3, Request());
        recorder.Record(Site, "forms", 3, Request(ip: "198.51.100.7"));

        Drain(channel).Should().HaveCount(2);
    }

    [Fact]
    public void Caps_searches_per_client()
    {
        var (recorder, channel) = Create(o => o.MaxSearchesPerClientPerHour = 2);

        recorder.Record(Site, "one", 1, Request());
        recorder.Record(Site, "two", 1, Request());
        recorder.Record(Site, "three", 1, Request());

        Drain(channel).Select(e => e.Term).Should().Equal("one", "two");
    }

    [Fact]
    public void Does_nothing_when_disabled()
    {
        var (recorder, channel) = Create(o => o.Enabled = false);

        recorder.Record(Site, "forms", 3, Request());

        Drain(channel).Should().BeEmpty();
    }
}

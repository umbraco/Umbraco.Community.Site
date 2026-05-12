using System.Text.Json;
using FluentAssertions;
using UmbracoCommunity.Web.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

public class CalendarFeedJsonTests
{
    private static CalendarFeed Deserialise(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        var json = File.ReadAllText(path);
        var result = JsonSerializer.Deserialize<CalendarFeed>(json, CalendarFeedJsonOptions.Default);
        result.Should().NotBeNull();
        return result!;
    }

    [Fact]
    public void Deserialises_feed_metadata()
    {
        var feed = Deserialise("calendar-feed-sample.json");

        feed.Feed.Title.Should().Be("Umbraco events on Meetup.com");
        feed.Feed.SourceUrl.Should().Be("https://umbracalendar.com/");
        feed.Feed.GeneratedAt.Should().Be(DateTimeOffset.Parse("2026-04-29T08:34:54Z"));
    }

    [Fact]
    public void Deserialises_three_events()
    {
        var feed = Deserialise("calendar-feed-sample.json");
        feed.Events.Should().HaveCount(3);
    }

    [Fact]
    public void Maps_event_fields_correctly()
    {
        var feed = Deserialise("calendar-feed-sample.json");
        var melbourne = feed.Events[0];

        melbourne.Id.Should().Be("312837198");
        melbourne.Url.Should().Be("https://www.meetup.com/australian-umbraco-meetups/events/312837198/");
        melbourne.Title.Should().Be("Umbraco Melbourne Meetup");
        melbourne.Summary.Should().Be("Some notes here");
        melbourne.StartsAt.Should().Be(DateTimeOffset.Parse("2026-07-16T18:00:00+10:00"));
        melbourne.EndsAt.Should().Be(DateTimeOffset.Parse("2026-07-16T19:30:00+10:00"));
        melbourne.Location.Should().Be("Luminary, 195 Little Collins Street, Melbourne, AU");
        melbourne.Organizer.Should().Be("Umbraco Melbourne Meetup");
        melbourne.AttendanceMode.Should().Be(AttendanceMode.InPerson);
        melbourne.IsHqOrganized.Should().BeFalse();
        melbourne.IsCancelled.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, AttendanceMode.InPerson)]
    [InlineData(1, AttendanceMode.Hybrid)]
    [InlineData(2, AttendanceMode.Online)]
    public void Maps_all_attendance_modes(int index, AttendanceMode expected)
    {
        var feed = Deserialise("calendar-feed-sample.json");
        feed.Events[index].AttendanceMode.Should().Be(expected);
    }

    [Fact]
    public void Allows_null_summary_location_and_organizer()
    {
        var feed = Deserialise("calendar-feed-sample.json");
        var codegarden = feed.Events[1];

        codegarden.Summary.Should().BeNull();
        codegarden.Location.Should().BeNull();
    }

    [Fact]
    public void Maps_isCancelled_and_isHqOrganized()
    {
        var feed = Deserialise("calendar-feed-sample.json");

        feed.Events[1].IsHqOrganized.Should().BeTrue();
        feed.Events[2].IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void Unknown_attendance_mode_falls_back_to_unknown()
    {
        const string json = """
        {
          "feed": { "title": "x", "sourceUrl": "https://x", "generatedAt": "2026-01-01T00:00:00Z" },
          "events": [{
            "id": "1", "url": "https://x", "title": "x", "summary": null,
            "publishedAt": "2026-01-01T00:00:00Z",
            "startsAt": "2026-01-01T00:00:00Z", "endsAt": "2026-01-01T01:00:00Z",
            "location": null, "organizer": null,
            "attendanceMode": "futureValueNotInEnum",
            "isHqOrganized": false, "isCancelled": false
          }]
        }
        """;
        var feed = JsonSerializer.Deserialize<CalendarFeed>(json, CalendarFeedJsonOptions.Default)!;
        feed.Events[0].AttendanceMode.Should().Be(AttendanceMode.Unknown);
    }
}

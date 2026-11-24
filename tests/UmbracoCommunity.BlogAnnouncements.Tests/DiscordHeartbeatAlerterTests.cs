using System.Text.Json;
using FluentAssertions;
using UmbracoCommunity.BlogAnnouncements.Monitoring;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class DiscordHeartbeatAlerterTests
{
    private static readonly DateTime LastRun = new(2026, 11, 18, 8, 46, 0, DateTimeKind.Utc);

    private static JsonElement BuildJson(TimeSpan age)
    {
        var alert = new StaleRunAlert(LastRun, age, LastRun.Add(age));
        var body = DiscordHeartbeatAlerter.BuildBody(alert);
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void BuildBody_StatesTheProblemAndWhenItLastRan()
    {
        var root = BuildJson(TimeSpan.FromHours(3));

        var embed = root.GetProperty("embeds")[0];
        embed.GetProperty("title").GetString().Should().Be("Blog announcements have stopped");
        embed.GetProperty("description").GetString().Should().Contain("3 hours");
        // The timestamp a responder needs to line up against deploys and logs.
        embed.GetProperty("description").GetString().Should().Contain("2026-11-18 08:46");
        root.GetProperty("username").GetString().Should().Be("Blog Announcements Monitor");
    }

    [Theory]
    [InlineData(90, "1 hour 30 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(45, "45 minutes")]
    [InlineData(2880, "2 days")]
    public void BuildBody_DescribesAgeInPlainWords(int minutes, string expected)
    {
        var root = BuildJson(TimeSpan.FromMinutes(minutes));

        root.GetProperty("embeds")[0].GetProperty("description").GetString()
            .Should().Contain(expected);
    }
}

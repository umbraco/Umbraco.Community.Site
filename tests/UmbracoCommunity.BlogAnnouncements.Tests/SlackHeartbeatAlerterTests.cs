using System.Text.Json;
using FluentAssertions;
using UmbracoCommunity.BlogAnnouncements.Monitoring;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class SlackHeartbeatAlerterTests
{
    private static readonly DateTime LastRun = new(2026, 11, 18, 8, 46, 0, DateTimeKind.Utc);

    private static JsonElement BuildJson(TimeSpan age)
    {
        var alert = new StaleRunAlert(LastRun, age, LastRun.Add(age));
        var body = SlackHeartbeatAlerter.BuildBody(alert);
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return JsonDocument.Parse(json).RootElement;
    }

    private static string DetailText(JsonElement root)
        => root.GetProperty("blocks")[1].GetProperty("text").GetProperty("text").GetString()!;

    [Fact]
    public void BuildBody_StatesTheProblemAndWhenItLastRan()
    {
        var root = BuildJson(TimeSpan.FromHours(3));

        root.GetProperty("blocks")[0].GetProperty("text").GetProperty("text").GetString()
            .Should().Be("Blog announcements have stopped");

        var detail = DetailText(root);
        detail.Should().Contain("3 hours");
        // The timestamp a responder needs to line up against deploys and logs.
        detail.Should().Contain("2026-11-18 08:46");
    }

    [Fact]
    public void BuildBody_SetsFallbackTextSoNotificationsAreReadable()
    {
        // Slack shows `text`, not the blocks, in push notifications and the sidebar preview.
        // Without it the alert arrives as "This content can't be displayed".
        var root = BuildJson(TimeSpan.FromHours(3));

        var text = root.GetProperty("text").GetString();
        text.Should().Contain("Blog announcements have stopped");
        text.Should().Contain("3 hours");
    }

    [Fact]
    public void BuildBody_UsesSlackMarkupNotDiscordMarkdown()
    {
        // Slack's mrkdwn is *single asterisk* for bold; ** renders literally.
        var root = BuildJson(TimeSpan.FromHours(3));

        root.GetProperty("blocks")[1].GetProperty("text").GetProperty("type").GetString()
            .Should().Be("mrkdwn");
        DetailText(root).Should().NotContain("**");
    }

    [Fact]
    public void BuildBody_HeaderIsPlainTextAsSlackRequires()
    {
        // A header block only accepts plain_text; mrkdwn there is rejected with invalid_blocks.
        var root = BuildJson(TimeSpan.FromHours(3));

        var header = root.GetProperty("blocks")[0];
        header.GetProperty("type").GetString().Should().Be("header");
        header.GetProperty("text").GetProperty("type").GetString().Should().Be("plain_text");
    }

    [Theory]
    [InlineData(90, "1 hour 30 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(45, "45 minutes")]
    [InlineData(2880, "2 days")]
    public void BuildBody_DescribesAgeInPlainWords(int minutes, string expected)
    {
        DetailText(BuildJson(TimeSpan.FromMinutes(minutes))).Should().Contain(expected);
    }
}

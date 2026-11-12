using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoCommunity.Web.Features.YouTube;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.YouTube;

public class YouTubeVideoValueTests
{
    private const string Json = """{"url":"https://youtu.be/dQw4w9WgXcQ","videoId":"dQw4w9WgXcQ","title":"Keynote","posterKey":"6753cbce-e0e9-4bc4-a812-9502dd378956"}""";

    [Fact]
    public void Reads_a_stored_json_string()
    {
        var value = YouTubeVideoValue.TryParse(Json)!;

        value.Url.Should().Be("https://youtu.be/dQw4w9WgXcQ");
        value.VideoId.Should().Be("dQw4w9WgXcQ");
        value.Title.Should().Be("Keynote");
        value.PosterKey.Should().Be(Guid.Parse("6753cbce-e0e9-4bc4-a812-9502dd378956"));
    }

    [Fact]
    public void Reads_values_nested_in_blocks()
    {
        YouTubeVideoValue.TryParse(JsonNode.Parse(Json))!.VideoId.Should().Be("dQw4w9WgXcQ");
        YouTubeVideoValue.TryParse(JsonDocument.Parse(Json).RootElement)!.VideoId.Should().Be("dQw4w9WgXcQ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void Returns_null_for_empty_or_broken_values(string? source)
    {
        YouTubeVideoValue.TryParse(source).Should().BeNull();
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var value = YouTubeVideoValue.TryParse(Json)!;

        YouTubeVideoValue.TryParse(value.ToJson()).Should().BeEquivalentTo(value);
    }

    [Fact]
    public void Embeds_through_youtube_nocookie()
    {
        new YouTubeVideo("https://youtu.be/dQw4w9WgXcQ?t=30", "dQw4w9WgXcQ", null, null).EmbedUrl
            .Should().Be("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=30");
    }
}

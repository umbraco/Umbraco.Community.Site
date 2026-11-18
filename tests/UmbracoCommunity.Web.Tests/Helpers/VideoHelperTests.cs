using FluentAssertions;
using UmbracoCommunity.Web.Helpers;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Helpers;

public class VideoHelperTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ&list=PL123")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    public void Finds_the_video_id(string url)
    {
        VideoHelper.GetYouTubeVideoId(url).Should().Be("dQw4w9WgXcQ");
    }

    [Theory]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("https://www.youtube.com/@UmbracoHQ")]
    [InlineData("https://www.youtube.com/watch?v=not-an-id")]
    [InlineData("not a url")]
    public void Rejects_anything_that_is_not_a_youtube_video(string url)
    {
        VideoHelper.GetYouTubeVideoId(url).Should().BeNull();
    }

    [Fact]
    public void Embeds_through_youtube_nocookie_without_the_js_api()
    {
        "https://www.youtube.com/watch?v=dQw4w9WgXcQ".GetYouTubeLink(null)
            .Should().Be("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ");
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=90")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=90s")]
    public void Keeps_the_start_time(string url)
    {
        url.GetYouTubeLink(null).Should().Be("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=90");
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=1m30s", 90)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=1h2m3s", 3723)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=2m", 120)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&start=45", 45)]
    public void Understands_youtube_start_time_formats(string url, int seconds)
    {
        url.GetYouTubeLink(null).Should().Be($"https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start={seconds}");
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=0")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=soon")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=1x30")]
    public void Ignores_a_missing_or_unreadable_start_time(string url)
    {
        url.GetYouTubeLink(null).Should().Be("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ");
    }

    [Fact]
    public void Returns_empty_for_an_unsupported_url()
    {
        "https://vimeo.com/123456".GetYouTubeLink(null).Should().BeEmpty();
    }
}

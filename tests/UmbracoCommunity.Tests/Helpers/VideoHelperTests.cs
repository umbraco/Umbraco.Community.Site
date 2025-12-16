using FluentAssertions;
using Moq;
using UmbracoCommunity.Web.Helpers;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Tests.Helpers;

public class VideoHelperTests
{
    [Fact]
    public void GetAllow_WithNullSettings_ReturnsDefaultAllows()
    {
        var result = ((SettingsVideoBlock?)null).GetAllow();

        result.Should().Contain("accelerometer");
        result.Should().Contain("clipboard-write");
        result.Should().Contain("encrypted-media");
        result.Should().Contain("fullscreen");
        result.Should().Contain("gyroscope");
        result.Should().Contain("picture-in-picture");
        result.Should().NotContain("autoplay");
    }

    [Fact]
    public void GetYouTubeLink_ConvertsWatchUrlToEmbed()
    {
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        var result = url.GetYouTubeLink(null);

        result.Should().Contain("youtube.com");
        result.Should().Contain("/embed/dQw4w9WgXcQ");
        result.Should().NotContain("watch?v=");
    }

    [Fact]
    public void GetYouTubeLink_ConvertsShortUrlToEmbed()
    {
        var url = "https://youtu.be/dQw4w9WgXcQ";

        var result = url.GetYouTubeLink(null);

        result.Should().Contain("youtube.com");
        result.Should().Contain("/embed/dQw4w9WgXcQ");
        result.Should().NotContain("youtu.be");
    }

    [Fact]
    public void GetYouTubeLink_PreservesEmbedUrl()
    {
        var url = "https://www.youtube.com/embed/dQw4w9WgXcQ";

        var result = url.GetYouTubeLink(null);

        result.Should().Contain("youtube.com");
        result.Should().Contain("/embed/dQw4w9WgXcQ");
    }
}

using FluentAssertions;
using UmbracoCommunity.Web.Helpers;

namespace UmbracoCommunity.Tests.Helpers;

public class ImageHelperTests
{
    [Theory]
    [InlineData("/media/image.jpg", "/media/image.jpg?format=webp")]
    [InlineData("/media/image.png", "/media/image.png?format=webp")]
    [InlineData("/media/image.jpg?width=100", "/media/image.jpg?width=100&format=webp")]
    [InlineData("/media/image.jpg?width=100&height=200", "/media/image.jpg?width=100&height=200&format=webp")]
    public void AppendWebpFormatter_AppendsFormatParameter(string input, string expected)
    {
        var result = input.AppendWebpFormatter();

        result.Should().Be(expected);
    }

    [Fact]
    public void AppendWebpFormatter_HandlesAbsoluteUrls()
    {
        var url = "https://example.com/media/image.jpg";

        var result = url.AppendWebpFormatter();

        result.Should().Contain("format=webp");
        result.Should().StartWith("https://example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AppendWebpFormatter_ReturnsInputForInvalidUrls(string? input)
    {
        var result = input.AppendWebpFormatter();

        result.Should().Be(input);
    }

    [Fact]
    public void AppendWebpFormatter_ReplacesExistingFormatParameter()
    {
        var url = "/media/image.jpg?format=png";

        var result = url.AppendWebpFormatter();

        result.Should().Be("/media/image.jpg?format=webp");
        result.Should().NotContain("format=png");
    }
}

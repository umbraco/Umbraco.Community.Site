using Umbraco.Community.NotFoundTracker.Matching;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/Foo", "/foo")]
    [InlineData("/Foo/Bar", "/foo/bar")]
    [InlineData("/FOO/BAR/", "/foo/bar/")]
    [InlineData("", "/")]
    [InlineData(null, "/")]
    public void NormalizePath_lowercases_and_keeps_leading_slash(string? input, string expected)
    {
        UrlNormalizer.NormalizePath(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("foo", "/foo")]
    [InlineData("//foo", "/foo")]
    [InlineData("///foo//bar", "/foo/bar")]
    public void NormalizePath_collapses_leading_and_duplicate_slashes(string input, string expected)
    {
        UrlNormalizer.NormalizePath(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Example.COM", "example.com")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("WWW.SITE.NET", "www.site.net")]
    public void NormalizeHostname_lowercases_and_handles_null(string? input, string expected)
    {
        UrlNormalizer.NormalizeHostname(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizePath_truncates_paths_longer_than_2048()
    {
        var longPath = "/" + new string('a', 3000);
        var result = UrlNormalizer.NormalizePath(longPath);
        result.Length.Should().Be(2048);
        result.Should().StartWith("/aaa");
    }
}

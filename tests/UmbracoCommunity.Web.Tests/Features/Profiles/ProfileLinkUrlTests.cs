using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileLinkUrlTests
{
    [Theory]
    [InlineData("https://example.org/me")]
    [InlineData("  https://example.org/me  ")]
    public void Accepts_https_addresses(string url) => ProfileLinkUrl.TryParse(url, out _).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://example.org/me")]
    [InlineData("ftp://example.org/me")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/path")]
    [InlineData("https://user:secret@example.org/me")]
    public void Refuses_everything_else(string? url) => ProfileLinkUrl.TryParse(url, out _).Should().BeFalse();
}

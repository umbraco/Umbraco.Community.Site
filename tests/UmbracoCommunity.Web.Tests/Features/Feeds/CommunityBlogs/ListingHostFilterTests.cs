using FluentAssertions;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

public class ListingHostFilterTests
{
    [Theory]
    [InlineData("community.umbraco.com", "community.umbraco.com")]
    [InlineData("WWW.Community.Umbraco.com", "community.umbraco.com")]
    [InlineData("https://www.community.umbraco.com/", "community.umbraco.com")]
    [InlineData("community.umbraco.com.", "community.umbraco.com")]
    [InlineData("localhost:44317/en", "localhost")]
    [InlineData("https://localhost:44317", "localhost")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void NormalizeHost_extracts_normalized_host(string? input, string? expected)
        => ListingHostFilter.NormalizeHost(input).Should().Be(expected);

    [Theory]
    [InlineData("https://community.umbraco.com/blog/post", true)]
    [InlineData("https://www.COMMUNITY.umbraco.com/blog/post", true)]
    [InlineData("http://community.umbraco.com:8080/x", true)]
    [InlineData("https://sub.community.umbraco.com/x", false)]
    [InlineData("https://other.dev/x", false)]
    [InlineData("not a url", false)]
    [InlineData("/relative/path", false)]
    [InlineData(null, false)]
    public void IsExcluded_matches_host_case_insensitively_ignoring_www(string? url, bool expected)
        => ListingHostFilter.IsExcluded(url, ["community.umbraco.com"]).Should().Be(expected);

    [Theory]
    [InlineData("WWW.Community.Umbraco.com")]
    [InlineData("https://www.community.umbraco.com/")]
    [InlineData(" community.umbraco.com ")]
    public void IsExcluded_normalizes_configured_hosts(string configured)
        => ListingHostFilter.IsExcluded("https://community.umbraco.com/blog/post", [configured]).Should().BeTrue();

    [Fact]
    public void IsExcluded_with_no_configured_hosts_is_false()
    {
        ListingHostFilter.IsExcluded("https://community.umbraco.com/x", []).Should().BeFalse();
        ListingHostFilter.IsExcluded("https://community.umbraco.com/x", null).Should().BeFalse();
        ListingHostFilter.IsExcluded("https://community.umbraco.com/x", [" ", ""]).Should().BeFalse();
    }
}

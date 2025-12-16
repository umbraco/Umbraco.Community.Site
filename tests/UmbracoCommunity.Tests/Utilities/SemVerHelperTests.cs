using FluentAssertions;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Tests.Utilities;

public class SemVerHelperTests
{
    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("1.0", true)]
    [InlineData("1.0.0-rc1", true)]
    [InlineData("1.0.0-beta.1", true)]
    [InlineData("1.0.0+build.123", true)]
    [InlineData("1.0.0-rc1+build.123", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("invalid", false)]
    [InlineData("v1.0.0", false)]
    public void IsValidSemVer_ReturnsExpectedResult(string? version, bool expected)
    {
        var result = SemVerHelper.IsValidSemVer(version!);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0-rc1", true)]
    [InlineData("1.0.0-beta.1", true)]
    [InlineData("1.0.0-alpha", true)]
    [InlineData("1.0.0-rc1+build.123", true)]
    [InlineData("1.0.0", false)]
    [InlineData("1.0.0+build.123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPreRelease_ReturnsExpectedResult(string? version, bool expected)
    {
        var result = SemVerHelper.IsPreRelease(version!);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0-rc1", "1.0.0")]
    [InlineData("1.0.0+build.123", "1.0.0")]
    [InlineData("1.0.0-rc1+build.123", "1.0.0")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void GetStableVersion_ReturnsExpectedResult(string? version, string expected)
    {
        var result = SemVerHelper.GetStableVersion(version!);

        result.Should().Be(expected);
    }

    [Fact]
    public void Parse_WithFullVersion_ReturnsAllComponents()
    {
        var (stableVersion, preRelease, buildMetadata) = SemVerHelper.Parse("1.0.0-rc1+build.123");

        stableVersion.Should().Be("1.0.0");
        preRelease.Should().Be("rc1");
        buildMetadata.Should().Be("build.123");
    }

    [Fact]
    public void Parse_WithStableVersion_ReturnsOnlyStableVersion()
    {
        var (stableVersion, preRelease, buildMetadata) = SemVerHelper.Parse("1.0.0");

        stableVersion.Should().Be("1.0.0");
        preRelease.Should().BeNull();
        buildMetadata.Should().BeNull();
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsEmptyStableVersion()
    {
        var (stableVersion, preRelease, buildMetadata) = SemVerHelper.Parse("");

        stableVersion.Should().BeEmpty();
        preRelease.Should().BeNull();
        buildMetadata.Should().BeNull();
    }
}

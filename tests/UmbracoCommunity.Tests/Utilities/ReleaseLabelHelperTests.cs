using FluentAssertions;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Tests.Utilities;

public class ReleaseLabelHelperTests
{
    [Theory]
    [InlineData("release/1.0.0", true)]
    [InlineData("release/17.0.0", true)]
    [InlineData("cms/release/1.0.0", true)]
    [InlineData("forms/release/1.0.0", true)]
    [InlineData("Release/1.0.0", true)]
    [InlineData("RELEASE/1.0.0", true)]
    [InlineData("bug", false)]
    [InlineData("feature", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("release", false)]
    public void IsReleaseLabel_ReturnsExpectedResult(string? label, bool expected)
    {
        var result = ReleaseLabelHelper.IsReleaseLabel(label!);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("release/1.0.0", "1.0.0")]
    [InlineData("release/17.0.0", "17.0.0")]
    [InlineData("cms/release/1.0.0", "1.0.0")]
    [InlineData("forms/release/14.2.0", "14.2.0")]
    [InlineData("release/1.0.0-rc1", "1.0.0-rc1")]
    [InlineData("release/1.0.0-beta.1", "1.0.0-beta.1")]
    public void ExtractVersion_ReturnsVersionFromLabel(string label, string expectedVersion)
    {
        var result = ReleaseLabelHelper.ExtractVersion(label);

        result.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData("release/1.0.0", true)]
    [InlineData("release/17.0.0", true)]
    [InlineData("release/1.0.0-rc1", true)]
    [InlineData("cms/release/1.0.0", true)]
    [InlineData("release/invalid", false)]
    [InlineData("bug", false)]
    public void HasValidSemVer_ReturnsExpectedResult(string label, bool expected)
    {
        var result = ReleaseLabelHelper.HasValidSemVer(label);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("release/1.0.0", "release/1.0.0")]
    [InlineData("cms/release/17.0.0", "release/17.0.0")]
    [InlineData("forms/release/14.2.0", "release/14.2.0")]
    [InlineData("commerce/release/1.0.0", "release/1.0.0")]
    public void Normalize_RemovesPrefixFromLabel(string label, string expectedNormalized)
    {
        var result = ReleaseLabelHelper.Normalize(label);

        result.Should().Be(expectedNormalized);
    }

    [Fact]
    public void IsValidReleaseLabelForRepository_AllowsUnprefixedLabels()
    {
        var result = ReleaseLabelHelper.IsValidReleaseLabelForRepository("release/1.0.0", null);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidReleaseLabelForRepository_AllowsPrefixedLabelsWhenConfigured()
    {
        var repoConfig = new RepositoryConfig
        {
            Name = "test",
            AnnouncementsPrefix = "cms"
        };

        var result = ReleaseLabelHelper.IsValidReleaseLabelForRepository("cms/release/1.0.0", repoConfig);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidReleaseLabelForRepository_RejectsPrefixedLabelsWhenNotConfigured()
    {
        var repoConfig = new RepositoryConfig
        {
            Name = "test",
            AnnouncementsPrefix = null
        };

        var result = ReleaseLabelHelper.IsValidReleaseLabelForRepository("forms/release/1.0.0", repoConfig);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidReleaseLabelForRepository_RejectsMismatchedPrefix()
    {
        var repoConfig = new RepositoryConfig
        {
            Name = "test",
            AnnouncementsPrefix = "cms"
        };

        var result = ReleaseLabelHelper.IsValidReleaseLabelForRepository("forms/release/1.0.0", repoConfig);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("release/1.0.0", true)]
    [InlineData("release/invalid", false)]
    [InlineData("bug", false)]
    public void IsValidReleaseLabelWithSemVer_CombinesChecks(string label, bool expected)
    {
        var result = ReleaseLabelHelper.IsValidReleaseLabelWithSemVer(label, null);

        result.Should().Be(expected);
    }
}

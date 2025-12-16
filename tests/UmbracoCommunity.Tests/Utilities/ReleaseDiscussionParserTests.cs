using FluentAssertions;
using UmbracoCommunity.Web.Features.GitHubSync.Models;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Tests.Utilities;

public class ReleaseDiscussionParserTests
{
    private readonly ReleaseDiscussionParser _parser = new();

    [Fact]
    public void ParseReleaseInfo_WithValidDiscussion_ReturnsViewModel()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: """
                **Release date:** 2024-06-15

                This is a major release with many improvements.

                ### Links
                - [Release notes](https://example.com)
                """);

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().NotBeNull();
        result!.Version.Should().Be("17.0.0");
        result.ReleaseLabel.Should().Be("release/17.0.0");
        result.ReleaseDate.Should().Be(new DateTime(2024, 6, 15));
        result.IsReleaseDateTba.Should().BeFalse();
        result.IsLts.Should().BeFalse();
    }

    [Fact]
    public void ParseReleaseInfo_WithLtsVersion_SetsIsLtsTrue()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: """
                **Release date:** 2024-06-15
                **Long term supported version**? Yes

                ### Links
                """);

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().NotBeNull();
        result!.IsLts.Should().BeTrue();
    }

    [Fact]
    public void ParseReleaseInfo_WithTodoDate_SetsIsReleaseDateTbaTrue()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: """
                **Release date:** TODO (2024-06-15)

                ### Links
                """);

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().NotBeNull();
        result!.IsReleaseDateTba.Should().BeTrue();
        result.ReleaseDate.Should().Be(new DateTime(2024, 6, 15));
    }

    [Fact]
    public void ParseReleaseInfo_WithoutReleaseLabel_ReturnsNull()
    {
        var discussion = CreateDiscussion(
            labels: ["bug", "feature"],
            body: "Some content");

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseReleaseInfo_WithInvalidSemVer_ReturnsNull()
    {
        var discussion = CreateDiscussion(
            labels: ["release/invalid"],
            body: "Some content");

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseReleaseInfo_ExtractsDescription()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: """
                **Release date:** 2024-06-15

                This is the description.
                It can span multiple lines.

                ### Links
                - [Release notes](https://example.com)
                """);

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().NotBeNull();
        result!.Description.Should().Contain("This is the description");
        result.Description.Should().Contain("It can span multiple lines");
        result.Description.Should().NotContain("### Links");
    }

    [Fact]
    public void ParseReleaseInfo_ExcludesLtsLineFromDescription()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: """
                **Release date:** 2024-06-15
                **Long term supported version**? Yes

                This is the description.

                ### Links
                """);

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().NotBeNull();
        result!.Description.Should().NotContain("Long term supported version");
    }

    [Fact]
    public void ParseReleaseInfo_SetsDiscussionUrl()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: "**Release date:** 2024-06-15",
            url: "https://github.com/umbraco/Umbraco-CMS/discussions/123");

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().NotBeNull();
        result!.DiscussionUrl.Should().Be("https://github.com/umbraco/Umbraco-CMS/discussions/123");
    }

    [Fact]
    public void ParseReleaseDiscussion_IncludesStatsFromDictionary()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: "**Release date:** 2024-06-15");

        var stats = new Dictionary<string, (int features, int issues, int breaking)>
        {
            ["release/17.0.0"] = (10, 25, 3)
        };

        var result = _parser.ParseReleaseDiscussion(discussion, stats);

        result.Should().NotBeNull();
        result!.FeatureCount.Should().Be(10);
        result.IssueCount.Should().Be(25);
        result.BreakingChangesCount.Should().Be(3);
    }

    [Fact]
    public void ParseReleaseDiscussion_WithNoStats_ReturnsZeroCounts()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0"],
            body: "**Release date:** 2024-06-15");

        var stats = new Dictionary<string, (int features, int issues, int breaking)>();

        var result = _parser.ParseReleaseDiscussion(discussion, stats);

        result.Should().NotBeNull();
        result!.FeatureCount.Should().Be(0);
        result.IssueCount.Should().Be(0);
        result.BreakingChangesCount.Should().Be(0);
    }

    [Fact]
    public void ParseReleaseInfo_WithPreReleaseVersion_ParsesCorrectly()
    {
        var discussion = CreateDiscussion(
            labels: ["release/17.0.0-rc1"],
            body: "**Release date:** 2024-06-15");

        var result = _parser.ParseReleaseInfo(discussion);

        result.Should().NotBeNull();
        result!.Version.Should().Be("17.0.0-rc1");
    }

    private static GitHubDiscussion CreateDiscussion(
        List<string> labels,
        string body,
        string url = "https://github.com/test/repo/discussions/1")
    {
        return new GitHubDiscussion
        {
            Id = "test-id",
            Title = "Test Discussion",
            Number = 1,
            Url = url,
            Body = body,
            CreatedAt = DateTime.UtcNow,
            Labels = labels
        };
    }
}

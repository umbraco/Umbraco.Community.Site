using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Utilities;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Tests.ViewModelBuilders;

public class ReleasePageViewModelBuilderTests
{
    private readonly Mock<IGitHubDataStore> _mockDataStore;
    private readonly Mock<IOptions<GitHubSyncOptions>> _mockOptions;
    private readonly GitHubSyncOptions _syncOptions;
    private readonly ReleaseDiscussionParser _releaseParser;

    public ReleasePageViewModelBuilderTests()
    {
        _mockDataStore = new Mock<IGitHubDataStore>();
        _mockOptions = new Mock<IOptions<GitHubSyncOptions>>();
        _releaseParser = new ReleaseDiscussionParser();

        _syncOptions = new GitHubSyncOptions
        {
            Organization = "umbraco",
            Repositories = new List<RepositoryConfig>
            {
                new()
                {
                    Name = "Umbraco-CMS",
                    NuGetPackageId = "Umbraco.Cms"
                }
            }
        };
        _mockOptions.Setup(x => x.Value).Returns(_syncOptions);

        // Default setup for data store methods
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubPullRequest>());
        _mockDataStore.Setup(x => x.GetIssuesByLabelPattern(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubIssue>());
        _mockDataStore.Setup(x => x.GetDiscussionsByCategory(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubDiscussion>());
        _mockDataStore.Setup(x => x.GetFirstTimeContributorPrNumbers(It.IsAny<string>()))
            .Returns(new Dictionary<string, int>());
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions(It.IsAny<string>()))
            .Returns(new Dictionary<string, DateTime>());
    }

    #region Build Method Tests

    [Fact]
    public void Build_WithVersion_SetsBasicProperties()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.Organization.Should().Be("umbraco");
        result.Repository.Should().Be("Umbraco-CMS");
        result.Version.Should().Be("17.0.0");
        result.ReleaseLabel.Should().Be("release/17.0.0");
    }

    [Fact]
    public void Build_WithPreReleaseVersion_SetsPreReleaseFlag()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0-rc1");

        result.IsPreRelease.Should().BeTrue();
        result.StableVersion.Should().Be("17.0.0");
    }

    [Fact]
    public void Build_WithStableVersion_SetsPreReleaseFlagToFalse()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.IsPreRelease.Should().BeFalse();
        result.StableVersion.Should().Be("17.0.0");
    }

    [Fact]
    public void Build_WithPullRequests_CategorizesByLabels()
    {
        var prs = new List<GitHubPullRequest>
        {
            CreateTestPullRequest(1, "Feature PR", new[] { "release/17.0.0", "category/feature" }),
            CreateTestPullRequest(2, "Breaking PR", new[] { "release/17.0.0", "category/breaking" }),
            CreateTestPullRequest(3, "Bugfix PR", new[] { "release/17.0.0", "category/bugfix" }),
            CreateTestPullRequest(4, "Notable PR", new[] { "release/17.0.0", "category/notable" })
        };

        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/17.0.0"))
            .Returns(prs);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.Release.Should().NotBeNull();
        result.Release!.Categories.Should().NotBeEmpty();

        // Check that categories are created
        var categoryNames = result.Release.Categories.Select(c => c.CategoryName).ToList();
        categoryNames.Should().Contain("Notable features");
        categoryNames.Should().Contain("Breaking changes");
        categoryNames.Should().Contain("Other features");
        categoryNames.Should().Contain("Bugfixes");
    }

    [Fact]
    public void Build_WithHqMemberPRs_MarksAsHqMember()
    {
        var pr = CreateTestPullRequest(1, "HQ PR", new[] { "release/17.0.0" }, "hquser");
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/17.0.0"))
            .Returns(new[] { pr });
        _mockDataStore.Setup(x => x.IsHqMemberAtTime("hquser", It.IsAny<DateTime>()))
            .Returns(true);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.Release.Should().NotBeNull();
        result.Release!.PullRequests.Should().HaveCount(1);
        result.Release.PullRequests[0].IsHqMember.Should().BeTrue();
        result.Release.PullRequests[0].AvatarUrl.Should().BeNull(); // No avatar for HQ members
    }

    [Fact]
    public void Build_WithExternalContributorPRs_SetsAvatarUrl()
    {
        var pr = CreateTestPullRequest(1, "External PR", new[] { "release/17.0.0" }, "contributor");
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/17.0.0"))
            .Returns(new[] { pr });
        _mockDataStore.Setup(x => x.IsHqMemberAtTime("contributor", It.IsAny<DateTime>()))
            .Returns(false);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.Release.Should().NotBeNull();
        result.Release!.PullRequests.Should().HaveCount(1);
        result.Release.PullRequests[0].IsHqMember.Should().BeFalse();
        result.Release.PullRequests[0].AvatarUrl.Should().Be("https://github.com/contributor.png");
    }

    [Fact]
    public void Build_WithFirstTimeContributor_SetsFirstTimeContributorFlag()
    {
        var pr = CreateTestPullRequest(1, "First PR", new[] { "release/17.0.0" }, "newcontributor");
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/17.0.0"))
            .Returns(new[] { pr });
        _mockDataStore.Setup(x => x.IsHqMemberAtTime("newcontributor", It.IsAny<DateTime>()))
            .Returns(false);
        _mockDataStore.Setup(x => x.GetFirstTimeContributorPrNumbers("Umbraco-CMS"))
            .Returns(new Dictionary<string, int> { ["newcontributor"] = 1 });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.Release.Should().NotBeNull();
        result.Release!.PullRequests.Should().HaveCount(1);
        result.Release.PullRequests[0].IsFirstTimeContributor.Should().BeTrue();
    }

    [Fact]
    public void Build_WithIssues_IncludesIssuesInRelease()
    {
        var issues = new List<GitHubIssue>
        {
            CreateTestIssue(100, "Bug Issue", new[] { "release/17.0.0" })
        };

        _mockDataStore.Setup(x => x.GetIssuesByLabelPattern("Umbraco-CMS", "release/17.0.0"))
            .Returns(issues);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.Release.Should().NotBeNull();
        result.Release!.PullRequests.Should().HaveCount(1);
        result.Release.PullRequests[0].Number.Should().Be(100);
    }

    [Fact]
    public void Build_WithInvalidSemVerLabel_SkipsPRs()
    {
        // PRs with invalid semver labels should be skipped
        var prs = new List<GitHubPullRequest>
        {
            CreateTestPullRequest(1, "Valid PR", new[] { "release/17.0.0" }),
            CreateTestPullRequest(2, "Invalid PR", new[] { "release/invalid" })
        };

        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/17.0.0"))
            .Returns(prs);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        // Only the valid PR should be included
        result.Release.Should().NotBeNull();
        result.Release!.PullRequests.Should().HaveCount(1);
        result.Release.PullRequests[0].Number.Should().Be(1);
    }

    [Fact]
    public void Build_WithNuGetVersion_SetsNuGetAvailability()
    {
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime> { ["17.0.0"] = DateTime.UtcNow.AddDays(-5) });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.ReleaseInfo.Should().NotBeNull();
        result.ReleaseInfo!.IsAvailableOnNuGet.Should().BeTrue();
    }

    #endregion

    #region Category Priority Tests

    [Fact]
    public void Build_PRWithMultipleCategories_PrioritizesNotableOverOther()
    {
        // A PR with both notable and feature labels should go to Notable features
        var pr = CreateTestPullRequest(1, "Notable Feature", new[] { "release/17.0.0", "category/notable", "category/feature" });
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/17.0.0"))
            .Returns(new[] { pr });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.Release.Should().NotBeNull();
        var notableCategory = result.Release!.Categories.FirstOrDefault(c => c.CategoryName == "Notable features");
        notableCategory.Should().NotBeNull();
        notableCategory!.PullRequests.Should().HaveCount(1);

        // Should NOT be in Other features as well
        var otherCategory = result.Release.Categories.FirstOrDefault(c => c.CategoryName == "Other features");
        otherCategory.Should().BeNull();
    }

    #endregion

    #region NuGet Package ID Tests

    [Fact]
    public void Build_WithConfiguredNuGetPackage_SetsNuGetPackageId()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.NuGetPackageId.Should().Be("Umbraco.Cms");
    }

    [Fact]
    public void Build_WithUnknownRepository_NuGetPackageIdIsNull()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Unknown-Repo",
            "17.0.0");

        result.NuGetPackageId.Should().BeNull();
    }

    #endregion

    #region Release Info Tests

    [Fact]
    public void Build_WithNoDiscussionOrNuGet_CreatesMinimalReleaseInfo()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(
            mockPage.Object,
            Mock.Of<IUmbracoContext>(),
            "umbraco",
            "Umbraco-CMS",
            "17.0.0");

        result.ReleaseInfo.Should().NotBeNull();
        result.ReleaseInfo!.Version.Should().Be("17.0.0");
        result.ReleaseInfo.ReleaseLabel.Should().Be("release/17.0.0");
    }

    #endregion

    #region Helper Methods

    private ReleasePageViewModelBuilder CreateBuilder()
    {
        return new ReleasePageViewModelBuilder(
            _mockDataStore.Object,
            _mockOptions.Object,
            _releaseParser);
    }

    private static Mock<IPublishedContent> CreateMockPublishedContent()
    {
        var mockContentType = new Mock<IPublishedContentType>();
        mockContentType.Setup(x => x.Alias).Returns("release");

        var mockContent = new Mock<IPublishedContent>();
        mockContent.Setup(x => x.ContentType).Returns(mockContentType.Object);
        mockContent.Setup(x => x.Id).Returns(1);
        mockContent.Setup(x => x.Name).Returns("Release Page");

        return mockContent;
    }

    private static GitHubPullRequest CreateTestPullRequest(int number, string title, string[] labels, string authorLogin = "contributor")
    {
        return new GitHubPullRequest
        {
            Id = $"PR_{number}",
            Number = number,
            Title = title,
            Url = $"https://github.com/umbraco/Umbraco-CMS/pull/{number}",
            State = "merged",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            MergedAt = DateTime.UtcNow.AddDays(-1),
            Repository = new GitHubRepository { Name = "Umbraco-CMS" },
            Author = new GitHubAuthor { Login = authorLogin, Name = "Test Contributor" },
            Labels = labels.ToList()
        };
    }

    private static GitHubIssue CreateTestIssue(int number, string title, string[] labels, string authorLogin = "reporter")
    {
        return new GitHubIssue
        {
            Id = $"I_{number}",
            Number = number,
            Title = title,
            Url = $"https://github.com/umbraco/Umbraco-CMS/issues/{number}",
            State = "closed",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            Repository = new GitHubRepository { Name = "Umbraco-CMS" },
            Author = new GitHubAuthor { Login = authorLogin, Name = "Test Reporter" },
            Labels = labels.ToList()
        };
    }

    #endregion
}

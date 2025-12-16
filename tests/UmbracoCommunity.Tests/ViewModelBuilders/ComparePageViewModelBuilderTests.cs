using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;
using UmbracoCommunity.Web.Utilities;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Tests.ViewModelBuilders;

public class ComparePageViewModelBuilderTests
{
    private readonly Mock<IGitHubDataStore> _mockDataStore;
    private readonly Mock<IOptions<GitHubSyncOptions>> _mockOptions;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly IMemoryCache _memoryCache;
    private readonly ReleaseDiscussionParser _releaseParser;
    private readonly GitHubSyncOptions _syncOptions;

    public ComparePageViewModelBuilderTests()
    {
        _mockDataStore = new Mock<IGitHubDataStore>();
        _mockOptions = new Mock<IOptions<GitHubSyncOptions>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
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

        // Default setup
        _mockDataStore.Setup(x => x.GetDiscussionsByCategory(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubDiscussion>());
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions(It.IsAny<string>()))
            .Returns(new Dictionary<string, DateTime>());
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubPullRequest>());
        _mockDataStore.Setup(x => x.GetIssuesByLabelPattern(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubIssue>());
        _mockDataStore.Setup(x => x.GetFirstTimeContributorPrNumbers(It.IsAny<string>()))
            .Returns(new Dictionary<string, int>());

        SetupHttpContext();
    }

    private void SetupHttpContext(string? from = null, string? to = null, string? labelCheck = null, string? includePreReleases = null)
    {
        var queryCollection = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["from"] = from ?? string.Empty,
            ["to"] = to ?? string.Empty,
            ["labelCheck"] = labelCheck ?? string.Empty,
            ["includePreReleases"] = includePreReleases ?? string.Empty
        });

        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(x => x.Query).Returns(queryCollection);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Request).Returns(mockRequest.Object);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);
    }

    [Fact]
    public void Build_WithNoQueryParams_ReturnsEmptyVersionGroups()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.VersionGroups.Should().BeEmpty();
        result.FromVersion.Should().BeNull();
        result.ToVersion.Should().BeNull();
    }

    [Fact]
    public void Build_WithFromAndToVersions_SetsVersionRange()
    {
        SetupHttpContext(from: "17.0.0", to: "17.2.0");

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0"] = DateTime.UtcNow.AddDays(-20),
            ["17.2.0"] = DateTime.UtcNow.AddDays(-10)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.FromVersion.Should().Be("17.0.0");
        result.ToVersion.Should().Be("17.2.0");
        result.LowestVersion.Should().Be("17.0.0");
        result.HighestVersion.Should().Be("17.2.0");
    }

    [Fact]
    public void Build_WithReversedVersions_CorrectlyDeterminesLowestAndHighest()
    {
        SetupHttpContext(from: "17.2.0", to: "17.0.0");

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0"] = DateTime.UtcNow.AddDays(-20),
            ["17.2.0"] = DateTime.UtcNow.AddDays(-10)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        // Even though from > to, it should correctly identify lowest and highest
        result.LowestVersion.Should().Be("17.0.0");
        result.HighestVersion.Should().Be("17.2.0");
    }

    [Fact]
    public void Build_WithLabelCheckEnabled_SetsLabelCheckFlag()
    {
        SetupHttpContext(labelCheck: "true");

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LabelCheck.Should().BeTrue();
    }

    [Fact]
    public void Build_WithIncludePreReleasesEnabled_SetsFlag()
    {
        SetupHttpContext(includePreReleases: "true");

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.IncludePreReleases.Should().BeTrue();
    }

    [Fact]
    public void Build_WithVersionRange_GroupsPRsByVersion()
    {
        SetupHttpContext(from: "17.0.0", to: "17.2.0");

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0"] = DateTime.UtcNow.AddDays(-20),
            ["17.2.0"] = DateTime.UtcNow.AddDays(-10)
        };

        var prs = new List<GitHubPullRequest>
        {
            CreateTestPullRequest(1, "PR for 17.1.0", new[] { "release/17.1.0", "category/feature" }),
            CreateTestPullRequest(2, "PR for 17.2.0", new[] { "release/17.2.0", "category/bugfix" })
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/"))
            .Returns(prs);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        // Should have 2 version groups (17.1.0 and 17.2.0 - 17.0.0 is excluded as the "from" version)
        result.VersionGroups.Should().HaveCount(2);
    }

    [Fact]
    public void Build_CalculatesTotalCounts()
    {
        SetupHttpContext(from: "17.0.0", to: "17.1.0");

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0"] = DateTime.UtcNow.AddDays(-10)
        };

        var prs = new List<GitHubPullRequest>
        {
            CreateTestPullRequest(1, "Feature", new[] { "release/17.1.0", "category/feature" }),
            CreateTestPullRequest(2, "Breaking", new[] { "release/17.1.0", "category/breaking" }),
            CreateTestPullRequest(3, "Bugfix", new[] { "release/17.1.0", "category/bugfix" })
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/"))
            .Returns(prs);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.FeatureCount.Should().Be(1);
        result.BreakingChangesCount.Should().Be(1);
        result.IssuesAndTasksCount.Should().Be(1);
    }

    [Fact]
    public void Build_CreatesContributorsView()
    {
        SetupHttpContext(from: "17.0.0", to: "17.1.0");

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0"] = DateTime.UtcNow.AddDays(-10)
        };

        var prs = new List<GitHubPullRequest>
        {
            CreateTestPullRequest(1, "Feature", new[] { "release/17.1.0", "category/feature" })
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);
        _mockDataStore.Setup(x => x.GetPullRequestsByLabelPattern("Umbraco-CMS", "release/"))
            .Returns(prs);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.CombinedContributors.Should().NotBeNull();
        result.CombinedContributors!.ReleaseLabel.Should().Contain("after v17.0.0");
        result.CombinedContributors.ReleaseLabel.Should().Contain("17.1.0");
    }

    [Fact]
    public void Build_WithoutPreReleasesFlag_ExcludesPreReleaseVersions()
    {
        SetupHttpContext(from: "17.0.0", to: "17.2.0", includePreReleases: "false");

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0-rc1"] = DateTime.UtcNow.AddDays(-20),
            ["17.2.0"] = DateTime.UtcNow.AddDays(-10)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        // RC version should be excluded from available versions
        result.AvailableVersions.Should().NotContain(v => v.Version.Contains("-rc"));
    }

    private ComparePageViewModelBuilder CreateBuilder()
    {
        return new ComparePageViewModelBuilder(
            _mockDataStore.Object,
            _mockOptions.Object,
            _memoryCache,
            _mockHttpContextAccessor.Object,
            _releaseParser);
    }

    private static Mock<IPublishedContent> CreateMockPublishedContent()
    {
        var mockContentType = new Mock<IPublishedContentType>();
        mockContentType.Setup(x => x.Alias).Returns("compare");

        var mockContent = new Mock<IPublishedContent>();
        mockContent.Setup(x => x.ContentType).Returns(mockContentType.Object);
        mockContent.Setup(x => x.Id).Returns(1);
        mockContent.Setup(x => x.Name).Returns("Compare Releases");

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
}

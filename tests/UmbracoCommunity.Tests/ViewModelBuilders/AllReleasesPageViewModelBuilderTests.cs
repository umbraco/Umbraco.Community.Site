using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;
using UmbracoCommunity.Web.Utilities;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Tests.ViewModelBuilders;

public class AllReleasesPageViewModelBuilderTests
{
    private readonly Mock<IGitHubDataStore> _mockDataStore;
    private readonly Mock<IOptions<GitHubSyncOptions>> _mockOptions;
    private readonly IMemoryCache _memoryCache;
    private readonly ReleaseDiscussionParser _releaseParser;
    private readonly GitHubSyncOptions _syncOptions;

    public AllReleasesPageViewModelBuilderTests()
    {
        _mockDataStore = new Mock<IGitHubDataStore>();
        _mockOptions = new Mock<IOptions<GitHubSyncOptions>>();
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
    }

    [Fact]
    public void Build_WithNoReleases_ReturnsEmptyVersionGroups()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.VersionGroups.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithNuGetVersions_GroupsByMajorVersion()
    {
        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0"] = DateTime.UtcNow.AddDays(-15),
            ["16.0.0"] = DateTime.UtcNow.AddDays(-60)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.VersionGroups.Should().HaveCount(2);
        result.VersionGroups.Should().Contain(g => g.MajorVersion == 17);
        result.VersionGroups.Should().Contain(g => g.MajorVersion == 16);
    }

    [Fact]
    public void Build_WithMajorVersionGroup_SetsLatestRelease()
    {
        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0"] = DateTime.UtcNow.AddDays(-15),
            ["17.2.0"] = DateTime.UtcNow.AddDays(-5)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        var version17Group = result.VersionGroups.First(g => g.MajorVersion == 17);
        version17Group.LatestRelease.Should().NotBeNull();
        version17Group.LatestRelease!.Version.Should().Be("17.2.0");
    }

    [Fact]
    public void Build_ExcludesPreReleaseFromLatest()
    {
        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.1.0-rc1"] = DateTime.UtcNow.AddDays(-5)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        var version17Group = result.VersionGroups.First(g => g.MajorVersion == 17);
        version17Group.LatestRelease.Should().NotBeNull();
        version17Group.LatestRelease!.Version.Should().Be("17.0.0");
    }

    [Fact]
    public void Build_SortsMajorVersionsDescending()
    {
        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["15.0.0"] = DateTime.UtcNow.AddDays(-90),
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["16.0.0"] = DateTime.UtcNow.AddDays(-60)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.VersionGroups[0].MajorVersion.Should().Be(17);
        result.VersionGroups[1].MajorVersion.Should().Be(16);
        result.VersionGroups[2].MajorVersion.Should().Be(15);
    }

    [Fact]
    public void Build_SetOtherReleasesExcludingLatest()
    {
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

        var version17Group = result.VersionGroups.First(g => g.MajorVersion == 17);
        version17Group.OtherReleases.Should().HaveCount(2);
        version17Group.OtherReleases.Select(r => r.Version).Should().Contain(new[] { "17.0.0", "17.1.0" });
        version17Group.OtherReleases.Should().NotContain(r => r.Version == "17.2.0"); // Latest should not be in Other
    }

    [Fact]
    public void Build_ExcludesInvalidSemVerVersions()
    {
        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["invalid"] = DateTime.UtcNow.AddDays(-20)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        // Only valid version should be included
        result.VersionGroups.Should().HaveCount(1);
        result.VersionGroups[0].LatestRelease!.Version.Should().Be("17.0.0");
    }

    [Fact]
    public void Build_ExcludesFutureNuGetVersions()
    {
        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["18.0.0"] = DateTime.UtcNow.AddDays(30) // Future release
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(nugetVersions);

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        // Only released version should be included
        result.VersionGroups.Should().HaveCount(1);
        result.VersionGroups[0].MajorVersion.Should().Be(17);
    }

    [Fact]
    public void Build_CacheKeyIsCorrect()
    {
        AllReleasesPageViewModelBuilder.CacheKey.Should().Be("AllReleases_VersionGroups");
    }

    private AllReleasesPageViewModelBuilder CreateBuilder()
    {
        return new AllReleasesPageViewModelBuilder(
            _mockDataStore.Object,
            _mockOptions.Object,
            _memoryCache,
            _releaseParser);
    }

    private static Mock<IPublishedContent> CreateMockPublishedContent()
    {
        var mockContentType = new Mock<IPublishedContentType>();
        mockContentType.Setup(x => x.Alias).Returns("allReleases");

        var mockContent = new Mock<IPublishedContent>();
        mockContent.Setup(x => x.ContentType).Returns(mockContentType.Object);
        mockContent.Setup(x => x.Id).Returns(1);
        mockContent.Setup(x => x.Name).Returns("All Releases");

        return mockContent;
    }
}

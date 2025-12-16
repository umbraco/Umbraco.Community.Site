using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;
using UmbracoCommunity.Web.Utilities;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Tests.ViewModelBuilders;

public class ReleasesHomePageViewModelBuilderTests
{
    private readonly Mock<IGitHubDataStore> _mockDataStore;
    private readonly Mock<IOptions<GitHubSyncOptions>> _mockOptions;
    private readonly ReleaseDiscussionParser _releaseParser;
    private readonly GitHubSyncOptions _syncOptions;

    public ReleasesHomePageViewModelBuilderTests()
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

        // Default setup
        _mockDataStore.Setup(x => x.GetDiscussionsByCategory(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubDiscussion>());
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions(It.IsAny<string>()))
            .Returns(new Dictionary<string, DateTime>());
    }

    [Fact]
    public void Build_WithNoDiscussions_ReturnsEmptyTimeline()
    {
        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LatestRelease.Should().BeNull();
        result.LtsReleases.Should().BeEmpty();
        result.UpcomingReleases.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithReleasedVersion_SetsLatestRelease()
    {
        var discussion = CreateReleaseDiscussion("17.0.0", DateTime.UtcNow.AddDays(-10));

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(new[] { discussion });
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime> { ["17.0.0"] = DateTime.UtcNow.AddDays(-10) });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.Version.Should().Be("17.0.0");
    }

    [Fact]
    public void Build_WithMultipleVersions_SelectsHighestAsLatest()
    {
        var discussions = new[]
        {
            CreateReleaseDiscussion("16.0.0", DateTime.UtcNow.AddDays(-60)),
            CreateReleaseDiscussion("17.0.0", DateTime.UtcNow.AddDays(-30)),
            CreateReleaseDiscussion("17.1.0", DateTime.UtcNow.AddDays(-10))
        };

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(discussions);
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime>
            {
                ["16.0.0"] = DateTime.UtcNow.AddDays(-60),
                ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
                ["17.1.0"] = DateTime.UtcNow.AddDays(-10)
            });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.Version.Should().Be("17.1.0");
    }

    [Fact]
    public void Build_WithLtsVersion_AddsToLtsReleases()
    {
        var discussion = CreateReleaseDiscussion("13.0.0", DateTime.UtcNow.AddDays(-180), isLts: true);

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(new[] { discussion });
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime> { ["13.0.0"] = DateTime.UtcNow.AddDays(-180) });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LtsReleases.Should().HaveCount(1);
        result.LtsReleases[0].Version.Should().Be("13.0.0");
        result.LtsReleases[0].IsLts.Should().BeTrue();
    }

    [Fact]
    public void Build_WithMultipleLtsVersions_SortsByVersionDescending()
    {
        var discussions = new[]
        {
            CreateReleaseDiscussion("10.0.0", DateTime.UtcNow.AddDays(-365), isLts: true),
            CreateReleaseDiscussion("13.0.0", DateTime.UtcNow.AddDays(-180), isLts: true)
        };

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(discussions);
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime>
            {
                ["10.0.0"] = DateTime.UtcNow.AddDays(-365),
                ["13.0.0"] = DateTime.UtcNow.AddDays(-180)
            });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LtsReleases.Should().HaveCount(2);
        result.LtsReleases[0].Version.Should().Be("13.0.0"); // Higher version first
        result.LtsReleases[1].Version.Should().Be("10.0.0");
    }

    [Fact]
    public void Build_WithUpcomingRelease_AddsToUpcomingReleases()
    {
        var discussion = CreateReleaseDiscussion("18.0.0", DateTime.UtcNow.AddDays(30)); // Future date

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(new[] { discussion });
        // No NuGet version - not yet released

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.UpcomingReleases.Should().HaveCount(1);
        result.UpcomingReleases[0].Version.Should().Be("18.0.0");
    }

    [Fact]
    public void Build_ExcludesPreReleasesFromLatest()
    {
        var discussions = new[]
        {
            CreateReleaseDiscussion("17.0.0", DateTime.UtcNow.AddDays(-30)),
            CreateReleaseDiscussion("17.1.0-rc1", DateTime.UtcNow.AddDays(-5))
        };

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(discussions);
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime>
            {
                ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
                ["17.1.0-rc1"] = DateTime.UtcNow.AddDays(-5)
            });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        // Pre-release should not be the latest
        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.Version.Should().Be("17.0.0");
    }

    [Fact]
    public void Build_ExcludesPreReleasesFromLts()
    {
        var discussion = CreateReleaseDiscussion("13.0.0-rc1", DateTime.UtcNow.AddDays(-200), isLts: true);

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(new[] { discussion });
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime> { ["13.0.0-rc1"] = DateTime.UtcNow.AddDays(-200) });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LtsReleases.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithPreReleaseForUpcoming_SetsHasPreReleaseFlag()
    {
        var discussion = CreateReleaseDiscussion("18.0.0", DateTime.UtcNow.AddDays(30)); // Future

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(new[] { discussion });
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime>
            {
                ["18.0.0-rc1"] = DateTime.UtcNow.AddDays(-5) // Pre-release already available
            });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.UpcomingReleases.Should().HaveCount(1);
        result.UpcomingReleases[0].HasPreRelease.Should().BeTrue();
        result.UpcomingReleases[0].PreReleaseVersion.Should().Be("18.0.0-rc1");
    }

    [Fact]
    public void Build_SortsUpcomingByDateThenVersion()
    {
        var discussions = new[]
        {
            CreateReleaseDiscussion("18.0.0", DateTime.UtcNow.AddDays(60)),
            CreateReleaseDiscussion("17.3.0", DateTime.UtcNow.AddDays(15)),
            CreateReleaseDiscussion("19.0.0", null) // TBA
        };

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(discussions);
        // No NuGet versions - all upcoming

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.UpcomingReleases.Should().HaveCount(3);
        // Releases with dates come first, sorted by date
        result.UpcomingReleases[0].Version.Should().Be("17.3.0"); // Soonest date
        result.UpcomingReleases[1].Version.Should().Be("18.0.0"); // Later date
        result.UpcomingReleases[2].Version.Should().Be("19.0.0"); // TBA (no date)
    }

    [Fact]
    public void Build_MarksVersionAvailableOnNuGet()
    {
        var discussion = CreateReleaseDiscussion("17.0.0", DateTime.UtcNow.AddDays(-10));

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(new[] { discussion });
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime> { ["17.0.0"] = DateTime.UtcNow.AddDays(-10) });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.IsAvailableOnNuGet.Should().BeTrue();
    }

    [Fact]
    public void Build_UpdatesDiscussionWithNewerNuGetVersion()
    {
        // Discussion says 17.0.0, but NuGet has 17.1.0
        var discussion = CreateReleaseDiscussion("17.0.0", DateTime.UtcNow.AddDays(-30));

        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "releases"))
            .Returns(new[] { discussion });
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms"))
            .Returns(new Dictionary<string, DateTime>
            {
                ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
                ["17.1.0"] = DateTime.UtcNow.AddDays(-10)
            });

        var builder = CreateBuilder();
        var mockPage = CreateMockPublishedContent();

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        // The discussion should be updated to show the newer NuGet version
        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.ActualLatestVersion.Should().Be("17.1.0");
    }

    private ReleasesHomePageViewModelBuilder CreateBuilder()
    {
        return new ReleasesHomePageViewModelBuilder(
            _mockDataStore.Object,
            _mockOptions.Object,
            _releaseParser);
    }

    private static Mock<IPublishedContent> CreateMockPublishedContent()
    {
        var mockContentType = new Mock<IPublishedContentType>();
        mockContentType.Setup(x => x.Alias).Returns("releasesHome");

        var mockContent = new Mock<IPublishedContent>();
        mockContent.Setup(x => x.ContentType).Returns(mockContentType.Object);
        mockContent.Setup(x => x.Id).Returns(1);
        mockContent.Setup(x => x.Name).Returns("Releases");

        return mockContent;
    }

    private static GitHubDiscussion CreateReleaseDiscussion(string version, DateTime? releaseDate, bool isLts = false)
    {
        var dateString = releaseDate.HasValue
            ? $"**Release date**: {releaseDate.Value:MMMM d, yyyy}"
            : "**Release date**: TBA";

        var ltsString = isLts ? "**Type**: Long Term Support (LTS)" : "";

        return new GitHubDiscussion
        {
            Id = $"D_{version}",
            Number = 1,
            Title = $"Umbraco {version} Release",
            Url = $"https://github.com/umbraco/Umbraco-CMS/discussions/{version.Replace(".", "")}",
            Body = $@"## Umbraco {version}

{dateString}
{ltsString}

This is the release discussion for version {version}.",
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            Repository = new GitHubRepository { Name = "Umbraco-CMS" },
            CategoryId = "releases",
            CategoryName = "Releases",
            Labels = new List<string> { $"release/{version}" }
        };
    }
}

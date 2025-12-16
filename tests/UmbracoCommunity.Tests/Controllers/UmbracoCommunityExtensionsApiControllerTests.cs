using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Extensions.Controllers;
using UmbracoCommunity.Extensions.Models;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Tests.Controllers;

public class UmbracoCommunityExtensionsApiControllerTests
{
    private readonly Mock<IBackOfficeSecurityAccessor> _mockSecurityAccessor;
    private readonly Mock<IGitHubDataStore> _mockDataStore;
    private readonly Mock<IOptions<GitHubSyncOptions>> _mockOptions;
    private readonly UmbracoCommunityExtensionsApiController _controller;

    public UmbracoCommunityExtensionsApiControllerTests()
    {
        _mockSecurityAccessor = new Mock<IBackOfficeSecurityAccessor>();
        _mockDataStore = new Mock<IGitHubDataStore>();
        _mockOptions = new Mock<IOptions<GitHubSyncOptions>>();

        var syncOptions = new GitHubSyncOptions
        {
            Organization = "umbraco",
            Repositories = new List<RepositoryConfig>
            {
                new() { Name = "Umbraco-CMS", NuGetPackageId = "Umbraco.Cms" }
            }
        };
        _mockOptions.Setup(x => x.Value).Returns(syncOptions);

        _controller = new UmbracoCommunityExtensionsApiController(
            _mockSecurityAccessor.Object,
            _mockDataStore.Object,
            _mockOptions.Object);
    }

    #region Basic Endpoints

    [Fact]
    public void Ping_ReturnsExpectedResponse()
    {
        var result = _controller.Ping();

        result.Should().Be("Pong");
    }

    [Fact]
    public void WhatsTheTimeMrWolf_ReturnsCurrentTime()
    {
        var before = DateTime.Now;
        var result = _controller.WhatsTheTimeMrWolf();
        var after = DateTime.Now;

        result.Should().BeOnOrAfter(before);
        result.Should().BeOnOrBefore(after);
    }

    #endregion

    #region HQ Members - GET

    [Fact]
    public void GetHqMembers_ReturnsAllMembers()
    {
        var members = new List<GitHubHqMember>
        {
            new() { Id = "user1", Login = "user1", Name = "User One" },
            new() { Id = "user2", Login = "user2", Name = "User Two" }
        };
        _mockDataStore.Setup(x => x.GetAllHqMembers()).Returns(members);

        var result = _controller.GetHqMembers();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedMembers = okResult.Value.Should().BeAssignableTo<IEnumerable<GitHubHqMember>>().Subject;
        returnedMembers.Should().HaveCount(2);
    }

    [Fact]
    public void GetHqMember_WithValidId_ReturnsMember()
    {
        var member = new GitHubHqMember { Id = "testuser", Login = "testuser", Name = "Test User" };
        _mockDataStore.Setup(x => x.GetHqMemberByLogin("testuser")).Returns(member);

        var result = _controller.GetHqMember("testuser");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedMember = okResult.Value.Should().BeOfType<GitHubHqMember>().Subject;
        returnedMember.Login.Should().Be("testuser");
    }

    [Fact]
    public void GetHqMember_WithInvalidId_ReturnsNotFound()
    {
        _mockDataStore.Setup(x => x.GetHqMemberByLogin("nonexistent")).Returns((GitHubHqMember?)null);

        var result = _controller.GetHqMember("nonexistent");

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region HQ Members - CREATE

    [Fact]
    public void CreateHqMember_WithValidMember_ReturnsCreated()
    {
        var member = new GitHubHqMember { Login = "newuser", Name = "New User" };
        _mockDataStore.Setup(x => x.GetHqMemberByLogin("newuser")).Returns((GitHubHqMember?)null);
        _mockDataStore.Setup(x => x.UpsertHqMembers(It.IsAny<IEnumerable<GitHubHqMember>>()))
            .Returns(new GitHubSyncResult { Added = 1 });

        // After create, return the member
        _mockDataStore.SetupSequence(x => x.GetHqMemberByLogin("newuser"))
            .Returns((GitHubHqMember?)null)  // First call during exists check
            .Returns(new GitHubHqMember { Id = "newuser", Login = "newuser", Name = "New User" }); // Second call after create

        var result = _controller.CreateHqMember(member);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public void CreateHqMember_WithMissingLogin_ReturnsBadRequest()
    {
        var member = new GitHubHqMember { Name = "New User" };

        var result = _controller.CreateHqMember(member);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Login is required");
    }

    [Fact]
    public void CreateHqMember_WhenAlreadyExists_ReturnsBadRequest()
    {
        var existingMember = new GitHubHqMember { Id = "existinguser", Login = "existinguser", Name = "Existing" };
        var newMember = new GitHubHqMember { Login = "existinguser", Name = "New User" };
        _mockDataStore.Setup(x => x.GetHqMemberByLogin("existinguser")).Returns(existingMember);

        var result = _controller.CreateHqMember(newMember);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("A member with this login already exists");
    }

    #endregion

    #region HQ Members - UPDATE

    [Fact]
    public void UpdateHqMember_WithValidMember_ReturnsOk()
    {
        var existingMember = new GitHubHqMember { Id = "testuser", Login = "testuser", Name = "Old Name" };
        var updatedMember = new GitHubHqMember { Login = "testuser", Name = "New Name" };

        _mockDataStore.Setup(x => x.GetHqMemberByLogin("testuser")).Returns(existingMember);
        _mockDataStore.Setup(x => x.UpsertHqMembers(It.IsAny<IEnumerable<GitHubHqMember>>()))
            .Returns(new GitHubSyncResult { Updated = 1 });

        var result = _controller.UpdateHqMember("testuser", updatedMember);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void UpdateHqMember_WithNonExistentId_ReturnsNotFound()
    {
        _mockDataStore.Setup(x => x.GetHqMemberByLogin("nonexistent")).Returns((GitHubHqMember?)null);
        var member = new GitHubHqMember { Login = "nonexistent", Name = "Name" };

        var result = _controller.UpdateHqMember("nonexistent", member);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void UpdateHqMember_WithMissingLogin_ReturnsBadRequest()
    {
        var existingMember = new GitHubHqMember { Id = "testuser", Login = "testuser", Name = "Name" };
        _mockDataStore.Setup(x => x.GetHqMemberByLogin("testuser")).Returns(existingMember);

        var member = new GitHubHqMember { Name = "Name" }; // No login

        var result = _controller.UpdateHqMember("testuser", member);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Login is required");
    }

    [Fact]
    public void UpdateHqMember_WithDifferentLogin_ReturnsBadRequest()
    {
        var existingMember = new GitHubHqMember { Id = "testuser", Login = "testuser", Name = "Name" };
        _mockDataStore.Setup(x => x.GetHqMemberByLogin("testuser")).Returns(existingMember);

        var member = new GitHubHqMember { Login = "differentlogin", Name = "Name" };

        var result = _controller.UpdateHqMember("testuser", member);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Cannot change login");
    }

    #endregion

    #region HQ Members - DELETE

    [Fact]
    public void DeleteHqMember_WithValidId_ReturnsNoContent()
    {
        _mockDataStore.Setup(x => x.DeleteHqMemberByLogin("testuser")).Returns(true);

        var result = _controller.DeleteHqMember("testuser");

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void DeleteHqMember_WithNonExistentId_ReturnsNotFound()
    {
        _mockDataStore.Setup(x => x.DeleteHqMemberByLogin("nonexistent")).Returns(false);

        var result = _controller.DeleteHqMember("nonexistent");

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region HQ Members - Import

    [Fact]
    public void ImportHqMembers_WithValidMembers_ReturnsOk()
    {
        var members = new List<GitHubHqMember>
        {
            new() { Id = "user1", Login = "user1", Name = "User One" },
            new() { Id = "user2", Login = "user2", Name = "User Two" }
        };

        _mockDataStore.Setup(x => x.GetAllHqMembers()).Returns(new List<GitHubHqMember>());
        _mockDataStore.Setup(x => x.UpsertHqMembers(It.IsAny<IEnumerable<GitHubHqMember>>()))
            .Returns(new GitHubSyncResult { Added = 2 });

        var result = _controller.ImportHqMembers(members);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void ImportHqMembers_WithEmptyList_ReturnsBadRequest()
    {
        var result = _controller.ImportHqMembers(new List<GitHubHqMember>());

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("No members provided");
    }

    [Fact]
    public void ImportHqMembers_WithNullList_ReturnsBadRequest()
    {
        var result = _controller.ImportHqMembers(null!);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("No members provided");
    }

    [Fact]
    public void ImportHqMembers_ClearsExistingMembers()
    {
        var existingMembers = new List<GitHubHqMember>
        {
            new() { Id = "old1", Login = "old1", Name = "Old One" }
        };
        var newMembers = new List<GitHubHqMember>
        {
            new() { Id = "new1", Login = "new1", Name = "New One" }
        };

        _mockDataStore.Setup(x => x.GetAllHqMembers()).Returns(existingMembers);
        _mockDataStore.Setup(x => x.DeleteHqMemberByLogin("old1")).Returns(true);
        _mockDataStore.Setup(x => x.UpsertHqMembers(It.IsAny<IEnumerable<GitHubHqMember>>()))
            .Returns(new GitHubSyncResult { Added = 1 });

        _controller.ImportHqMembers(newMembers);

        _mockDataStore.Verify(x => x.DeleteHqMemberByLogin("old1"), Times.Once);
    }

    #endregion

    #region GitHub Data Export/Import

    [Fact]
    public void ExportGitHubData_ReturnsAllData()
    {
        var issues = new List<GitHubIssue> { CreateTestIssue(1, "Issue 1") };
        var prs = new List<GitHubPullRequest> { CreateTestPullRequest(1, "PR 1") };
        var discussions = new List<GitHubDiscussion> { CreateTestDiscussion("D1", "Discussion 1") };
        var nugetVersions = new Dictionary<string, DateTime> { ["17.0.0"] = DateTime.UtcNow };

        _mockDataStore.Setup(x => x.GetAllIssues("Umbraco-CMS")).Returns(issues);
        _mockDataStore.Setup(x => x.GetAllPullRequests("Umbraco-CMS")).Returns(prs);
        _mockDataStore.Setup(x => x.GetAllDiscussions("Umbraco-CMS")).Returns(discussions);
        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms")).Returns(nugetVersions);

        var result = _controller.ExportGitHubData();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var export = okResult.Value.Should().BeOfType<GitHubDataExport>().Subject;
        export.Issues.Should().HaveCount(1);
        export.PullRequests.Should().HaveCount(1);
        export.Discussions.Should().HaveCount(1);
    }

    [Fact]
    public void ImportGitHubData_WithValidData_ReturnsOk()
    {
        var data = new GitHubDataExport
        {
            Issues = new List<GitHubIssue> { CreateTestIssue(1, "Issue 1") },
            PullRequests = new List<GitHubPullRequest> { CreateTestPullRequest(1, "PR 1") },
            Discussions = new List<GitHubDiscussion> { CreateTestDiscussion("D1", "Discussion 1") }
        };

        _mockDataStore.Setup(x => x.ClearGitHubData());
        _mockDataStore.Setup(x => x.UpsertIssues(It.IsAny<IEnumerable<GitHubIssue>>()))
            .Returns(new GitHubSyncResult { Added = 1 });
        _mockDataStore.Setup(x => x.UpsertPullRequests(It.IsAny<IEnumerable<GitHubPullRequest>>()))
            .Returns(new GitHubSyncResult { Added = 1 });
        _mockDataStore.Setup(x => x.UpsertDiscussions(It.IsAny<IEnumerable<GitHubDiscussion>>()))
            .Returns(new GitHubSyncResult { Added = 1 });

        var result = _controller.ImportGitHubData(data);

        result.Should().BeOfType<OkObjectResult>();
        _mockDataStore.Verify(x => x.ClearGitHubData(), Times.Once);
    }

    [Fact]
    public void ImportGitHubData_WithNullData_ReturnsBadRequest()
    {
        var result = _controller.ImportGitHubData(null!);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("No data provided");
    }

    #endregion

    #region Contributions Endpoint

    [Fact]
    public void GetContributionStats_ReturnsCorrectStats()
    {
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        var prs = new List<GitHubPullRequest>
        {
            CreateTestPullRequest(1, "PR 1", mergedAt: startDate.AddDays(5)),
            CreateTestPullRequest(2, "PR 2", mergedAt: startDate.AddDays(10)),
            CreateTestPullRequest(3, "PR 3", mergedAt: startDate.AddDays(-10)) // Outside range
        };

        _mockDataStore.Setup(x => x.GetAllPullRequests("Umbraco-CMS")).Returns(prs);
        _mockDataStore.Setup(x => x.IsHqMemberAtTime(It.IsAny<string>(), It.IsAny<DateTime>())).Returns(false);

        var result = _controller.GetContributionStats(startDate, endDate);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ContributionStats>().Subject;
        stats.TotalExternalPullRequests.Should().Be(2); // Only PRs within date range
    }

    [Fact]
    public void GetContributionStats_ExcludesHqMembers()
    {
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var mergedDate = startDate.AddDays(5);

        var externalPr = CreateTestPullRequest(1, "External PR", authorLogin: "external_user", mergedAt: mergedDate);
        var hqPr = CreateTestPullRequest(2, "HQ PR", authorLogin: "hq_member", mergedAt: mergedDate);

        _mockDataStore.Setup(x => x.GetAllPullRequests("Umbraco-CMS")).Returns(new[] { externalPr, hqPr });
        _mockDataStore.Setup(x => x.IsHqMemberAtTime("external_user", mergedDate)).Returns(false);
        _mockDataStore.Setup(x => x.IsHqMemberAtTime("hq_member", mergedDate)).Returns(true);

        var result = _controller.GetContributionStats(startDate, endDate);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ContributionStats>().Subject;
        stats.TotalExternalPullRequests.Should().Be(1);
        stats.TotalExternalContributors.Should().Be(1);
    }

    #endregion

    #region Releases Endpoint

    [Fact]
    public void GetReleases_ReturnsReleasesInDateRange()
    {
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var releaseDate = startDate.AddDays(15);

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = releaseDate,
            ["16.0.0"] = startDate.AddDays(-60) // Outside range
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms")).Returns(nugetVersions);
        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "Releases"))
            .Returns(new List<GitHubDiscussion>());
        _mockDataStore.Setup(x => x.GetPullRequestsByRelease(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubPullRequest>());

        var result = _controller.GetReleases(startDate, endDate);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value.Should().BeOfType<ReleaseSummary>().Subject;
        summary.Releases.Should().HaveCount(1);
        summary.Releases[0].Version.Should().Be("17.0.0");
    }

    [Fact]
    public void GetReleases_IdentifiesPreReleaseVersions()
    {
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var releaseDate = startDate.AddDays(15);

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0-rc1"] = releaseDate
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms")).Returns(nugetVersions);
        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "Releases"))
            .Returns(new List<GitHubDiscussion>());
        _mockDataStore.Setup(x => x.GetPullRequestsByRelease(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubPullRequest>());

        var result = _controller.GetReleases(startDate, endDate);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value.Should().BeOfType<ReleaseSummary>().Subject;
        summary.Releases[0].IsPreRelease.Should().BeTrue();
    }

    [Fact]
    public void GetReleases_IdentifiesMajorVersions()
    {
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var releaseDate = startDate.AddDays(15);

        var nugetVersions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = releaseDate,
            ["17.1.0"] = releaseDate.AddDays(1)
        };

        _mockDataStore.Setup(x => x.GetNuGetPackageVersions("Umbraco.Cms")).Returns(nugetVersions);
        _mockDataStore.Setup(x => x.GetDiscussionsByCategory("Umbraco-CMS", "Releases"))
            .Returns(new List<GitHubDiscussion>());
        _mockDataStore.Setup(x => x.GetPullRequestsByRelease(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<GitHubPullRequest>());

        var result = _controller.GetReleases(startDate, endDate);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value.Should().BeOfType<ReleaseSummary>().Subject;

        var majorRelease = summary.Releases.First(r => r.Version == "17.0.0");
        majorRelease.IsMajor.Should().BeTrue();

        var minorRelease = summary.Releases.First(r => r.Version == "17.1.0");
        minorRelease.IsMajor.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static GitHubPullRequest CreateTestPullRequest(
        int number,
        string title,
        string authorLogin = "contributor",
        DateTime? mergedAt = null)
    {
        return new GitHubPullRequest
        {
            Id = $"PR_{number}",
            Number = number,
            Title = title,
            Url = $"https://github.com/umbraco/Umbraco-CMS/pull/{number}",
            State = "merged",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            MergedAt = mergedAt ?? DateTime.UtcNow.AddDays(-1),
            Repository = new GitHubRepository { Name = "Umbraco-CMS" },
            Author = new GitHubAuthor { Login = authorLogin, Name = "Test Contributor" },
            Labels = new List<string>()
        };
    }

    private static GitHubIssue CreateTestIssue(int number, string title)
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
            Author = new GitHubAuthor { Login = "reporter", Name = "Test Reporter" },
            Labels = new List<string>()
        };
    }

    private static GitHubDiscussion CreateTestDiscussion(string id, string title)
    {
        return new GitHubDiscussion
        {
            Id = id,
            Number = 1,
            Title = title,
            Url = "https://github.com/umbraco/Umbraco-CMS/discussions/1",
            Body = "Discussion body",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            Repository = new GitHubRepository { Name = "Umbraco-CMS" },
            CategoryId = "Releases",
            CategoryName = "Releases",
            Labels = new List<string>()
        };
    }

    #endregion
}

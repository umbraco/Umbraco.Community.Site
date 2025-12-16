using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Tests.Integration.DataStore;

/// <summary>
/// Integration tests for GitHubSqlStore using an in-memory SQLite database.
/// These tests verify the data access layer independently of HTTP/Hangfire.
/// </summary>
[TestFixture]
public class GitHubSqlStoreTests
{
    private TestDbContextFactory _contextFactory = null!;
    private GitHubSqlStore _store = null!;
    private IMemoryCache _cache = null!;

    [SetUp]
    public void SetUp()
    {
        _contextFactory = new TestDbContextFactory();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _store = new GitHubSqlStore(_contextFactory, _cache);
    }

    [TearDown]
    public void TearDown()
    {
        _contextFactory?.Dispose();
        _cache?.Dispose();
    }

    #region HQ Members Tests

    [Test]
    public void UpsertHqMembers_InsertsNewMember()
    {
        // Arrange
        var member = new GitHubHqMember
        {
            Id = "testuser",
            Login = "testuser",
            Name = "Test User"
        };

        // Act
        var result = _store.UpsertHqMembers(new[] { member });

        // Assert
        result.Added.Should().Be(1);
        result.Updated.Should().Be(0);

        var retrieved = _store.GetHqMemberByLogin("testuser");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test User");
    }

    [Test]
    public void UpsertHqMembers_UpdatesExistingMember()
    {
        // Arrange
        var member = new GitHubHqMember
        {
            Id = "testuser",
            Login = "testuser",
            Name = "Test User"
        };
        _store.UpsertHqMembers(new[] { member });

        // Act - Update the name
        member.Name = "Updated Name";
        var result = _store.UpsertHqMembers(new[] { member });

        // Assert
        result.Added.Should().Be(0);
        result.Updated.Should().Be(1);

        var retrieved = _store.GetHqMemberByLogin("testuser");
        retrieved!.Name.Should().Be("Updated Name");
    }

    [Test]
    public void GetAllHqMembers_ReturnsAllMembers()
    {
        // Arrange
        var members = new[]
        {
            new GitHubHqMember { Id = "user1", Login = "user1", Name = "User One" },
            new GitHubHqMember { Id = "user2", Login = "user2", Name = "User Two" },
            new GitHubHqMember { Id = "user3", Login = "user3", Name = "User Three" }
        };
        _store.UpsertHqMembers(members);

        // Act
        var result = _store.GetAllHqMembers().ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Select(m => m.Login).Should().Contain(new[] { "user1", "user2", "user3" });
    }

    [Test]
    public void DeleteHqMemberByLogin_RemovesMember()
    {
        // Arrange
        var member = new GitHubHqMember { Id = "testuser", Login = "testuser", Name = "Test" };
        _store.UpsertHqMembers(new[] { member });

        // Act
        var deleted = _store.DeleteHqMemberByLogin("testuser");

        // Assert
        deleted.Should().BeTrue();
        _store.GetHqMemberByLogin("testuser").Should().BeNull();
    }

    [Test]
    public void DeleteHqMemberByLogin_ReturnsFalseForNonExistent()
    {
        // Act
        var deleted = _store.DeleteHqMemberByLogin("nonexistent");

        // Assert
        deleted.Should().BeFalse();
    }

    [Test]
    public void IsHqMemberAtTime_ReturnsTrueForCurrentMember()
    {
        // Arrange
        var member = new GitHubHqMember
        {
            Id = "testuser",
            Login = "testuser",
            Name = "Test",
            Periods = new List<EmploymentPeriod>
            {
                new() { Start = DateTime.UtcNow.AddYears(-1), End = null }
            }
        };
        _store.UpsertHqMembers(new[] { member });

        // Act
        var isHqMember = _store.IsHqMemberAtTime("testuser", DateTime.UtcNow);

        // Assert
        isHqMember.Should().BeTrue();
    }

    [Test]
    public void IsHqMemberAtTime_ReturnsFalseForNonMember()
    {
        // Act
        var isHqMember = _store.IsHqMemberAtTime("nonexistent", DateTime.UtcNow);

        // Assert
        isHqMember.Should().BeFalse();
    }

    [Test]
    public void IsHqMemberAtTime_ReturnsFalseForFormerMember()
    {
        // Arrange - Member who left 6 months ago
        var member = new GitHubHqMember
        {
            Id = "formermember",
            Login = "formermember",
            Name = "Former",
            Periods = new List<EmploymentPeriod>
            {
                new()
                {
                    Start = DateTime.UtcNow.AddYears(-2),
                    End = DateTime.UtcNow.AddMonths(-6)
                }
            }
        };
        _store.UpsertHqMembers(new[] { member });

        // Act - Check if they were HQ member today
        var isHqMember = _store.IsHqMemberAtTime("formermember", DateTime.UtcNow);

        // Assert
        isHqMember.Should().BeFalse();
    }

    [Test]
    public void IsHqMemberAtTime_ReturnsTrueForDateDuringEmployment()
    {
        // Arrange - Member who left 6 months ago
        var member = new GitHubHqMember
        {
            Id = "formermember",
            Login = "formermember",
            Name = "Former",
            Periods = new List<EmploymentPeriod>
            {
                new()
                {
                    Start = DateTime.UtcNow.AddYears(-2),
                    End = DateTime.UtcNow.AddMonths(-6)
                }
            }
        };
        _store.UpsertHqMembers(new[] { member });

        // Act - Check if they were HQ member 1 year ago (during employment)
        var isHqMember = _store.IsHqMemberAtTime("formermember", DateTime.UtcNow.AddYears(-1));

        // Assert
        isHqMember.Should().BeTrue();
    }

    #endregion

    #region Pull Requests Tests

    [Test]
    public void UpsertPullRequests_InsertsNewPR()
    {
        // Arrange
        var pr = CreateTestPullRequest(123, "Test PR");

        // Act
        var result = _store.UpsertPullRequests(new[] { pr });

        // Assert
        result.Added.Should().Be(1);
        result.Updated.Should().Be(0);
    }

    [Test]
    public void GetAllPullRequests_ReturnsAllForRepository()
    {
        // Arrange
        var prs = new[]
        {
            CreateTestPullRequest(1, "PR 1"),
            CreateTestPullRequest(2, "PR 2"),
            CreateTestPullRequest(3, "PR 3")
        };
        _store.UpsertPullRequests(prs);

        // Act
        var result = _store.GetAllPullRequests("Umbraco-CMS").ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Test]
    public void GetPullRequestsByRelease_ReturnsFilteredPRs()
    {
        // Arrange
        var pr1 = CreateTestPullRequest(1, "PR for 17.0.0", "release/17.0.0");
        var pr2 = CreateTestPullRequest(2, "PR for 17.0.0", "release/17.0.0");
        var pr3 = CreateTestPullRequest(3, "PR for 16.0.0", "release/16.0.0");
        _store.UpsertPullRequests(new[] { pr1, pr2, pr3 });

        // Act
        var result = _store.GetPullRequestsByRelease("Umbraco-CMS", "release/17.0.0").ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Number).Should().Contain(new[] { 1, 2 });
    }

    #endregion

    #region Issues Tests

    [Test]
    public void UpsertIssues_InsertsNewIssue()
    {
        // Arrange
        var issue = CreateTestIssue(456, "Test Issue");

        // Act
        var result = _store.UpsertIssues(new[] { issue });

        // Assert
        result.Added.Should().Be(1);
        result.Updated.Should().Be(0);
    }

    [Test]
    public void GetAllIssues_ReturnsAllForRepository()
    {
        // Arrange
        var issues = new[]
        {
            CreateTestIssue(1, "Issue 1"),
            CreateTestIssue(2, "Issue 2")
        };
        _store.UpsertIssues(issues);

        // Act
        var result = _store.GetAllIssues("Umbraco-CMS").ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Test]
    public void GetIssuesByRelease_ReturnsFilteredIssues()
    {
        // Arrange
        var issue1 = CreateTestIssue(1, "Issue for 17.0.0", "release/17.0.0");
        var issue2 = CreateTestIssue(2, "Issue for 16.0.0", "release/16.0.0");
        _store.UpsertIssues(new[] { issue1, issue2 });

        // Act
        var result = _store.GetIssuesByRelease("Umbraco-CMS", "release/17.0.0").ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Number.Should().Be(1);
    }

    #endregion

    #region Discussions Tests

    [Test]
    public void UpsertDiscussions_InsertsNewDiscussion()
    {
        // Arrange
        var discussion = CreateTestDiscussion("D_123", "Release 17.0.0");

        // Act
        var result = _store.UpsertDiscussions(new[] { discussion });

        // Assert
        result.Added.Should().Be(1);
        result.Updated.Should().Be(0);
    }

    [Test]
    public void GetDiscussionsByCategory_ReturnsFilteredDiscussions()
    {
        // Arrange - Use unique numbers to avoid collision
        var discussion1 = CreateTestDiscussion("D_cat_1", "Release 17.0.0", "Releases", 101);
        var discussion2 = CreateTestDiscussion("D_cat_2", "General Q", "General", 102);
        _store.UpsertDiscussions(new[] { discussion1, discussion2 });

        // Act
        var result = _store.GetDiscussionsByCategory("Umbraco-CMS", "Releases").ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Release 17.0.0");
    }

    #endregion

    #region NuGet Package Versions Tests

    [Test]
    public void UpsertNuGetPackageVersions_InsertsVersions()
    {
        // Arrange
        var versions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.0.1"] = DateTime.UtcNow.AddDays(-15),
            ["17.1.0"] = DateTime.UtcNow
        };

        // Act
        var result = _store.UpsertNuGetPackageVersions("Umbraco.Cms", versions);

        // Assert
        result.Added.Should().Be(3);
    }

    [Test]
    public void GetNuGetPackageVersions_ReturnsStoredVersions()
    {
        // Arrange
        var versions = new Dictionary<string, DateTime>
        {
            ["17.0.0"] = DateTime.UtcNow.AddDays(-30),
            ["17.0.1"] = DateTime.UtcNow
        };
        _store.UpsertNuGetPackageVersions("Umbraco.Cms", versions);

        // Act
        var result = _store.GetNuGetPackageVersions("Umbraco.Cms");

        // Assert
        result.Should().HaveCount(2);
        result.Keys.Should().Contain(new[] { "17.0.0", "17.0.1" });
    }

    #endregion

    #region Clear Data Tests

    [Test]
    public void ClearGitHubData_RemovesAllGitHubData()
    {
        // Arrange
        _store.UpsertPullRequests(new[] { CreateTestPullRequest(1, "PR") });
        _store.UpsertIssues(new[] { CreateTestIssue(1, "Issue") });
        _store.UpsertDiscussions(new[] { CreateTestDiscussion("D_1", "Discussion") });

        // Act
        _store.ClearGitHubData();

        // Assert
        _store.GetAllPullRequests("Umbraco-CMS").Should().BeEmpty();
        _store.GetAllIssues("Umbraco-CMS").Should().BeEmpty();
        _store.GetAllDiscussions("Umbraco-CMS").Should().BeEmpty();
    }

    [Test]
    public void ClearGitHubData_PreservesHqMembers()
    {
        // Arrange
        _store.UpsertHqMembers(new[] { new GitHubHqMember { Id = "user", Login = "user", Name = "User" } });
        _store.UpsertPullRequests(new[] { CreateTestPullRequest(1, "PR") });

        // Act
        _store.ClearGitHubData();

        // Assert
        _store.GetAllHqMembers().Should().HaveCount(1);
        _store.GetAllPullRequests("Umbraco-CMS").Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static GitHubPullRequest CreateTestPullRequest(int number, string title, string? releaseLabel = null)
    {
        var labels = new List<string>();
        if (releaseLabel != null) labels.Add(releaseLabel);

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
            Author = new GitHubAuthor { Login = "contributor", Name = "Test Contributor" },
            Labels = labels
        };
    }

    private static GitHubIssue CreateTestIssue(int number, string title, string? releaseLabel = null)
    {
        var labels = new List<string>();
        if (releaseLabel != null) labels.Add(releaseLabel);

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
            Labels = labels
        };
    }

    private static GitHubDiscussion CreateTestDiscussion(string id, string title, string category = "Releases", int number = 1)
    {
        return new GitHubDiscussion
        {
            Id = id,
            Number = number,
            Title = title,
            Url = $"https://github.com/umbraco/Umbraco-CMS/discussions/{number}",
            Body = "Discussion body",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            Repository = new GitHubRepository { Name = "Umbraco-CMS" },
            CategoryId = category,
            CategoryName = category,
            Labels = new List<string> { $"release/{title.Replace("Release ", "")}" }
        };
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// In-memory DbContext factory for testing.
    /// </summary>
    private class TestDbContextFactory : IDbContextFactory<GitHubDbContext>, IDisposable
    {
        private readonly DbContextOptions<GitHubDbContext> _options;
        private GitHubDbContext? _context;

        public TestDbContextFactory()
        {
            // Use a unique database name for each test run
            var dbName = $"TestDb_{Guid.NewGuid()}";
            _options = new DbContextOptionsBuilder<GitHubDbContext>()
                .UseSqlite($"DataSource={dbName};Mode=Memory;Cache=Shared")
                .Options;

            // Create and open the connection to keep it alive
            _context = new GitHubDbContext(_options);
            _context.Database.OpenConnection();
            _context.Database.EnsureCreated();
        }

        public GitHubDbContext CreateDbContext()
        {
            return new GitHubDbContext(_options);
        }

        public void Dispose()
        {
            _context?.Database.CloseConnection();
            _context?.Dispose();
        }
    }

    #endregion
}

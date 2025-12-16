using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

/// <summary>
/// Interface for GitHub data store operations.
/// </summary>
public interface IGitHubDataStore
{
    // Pull Requests
    GitHubSyncResult UpsertPullRequests(IEnumerable<GitHubPullRequest> pullRequests);
    IEnumerable<GitHubPullRequest> GetAllPullRequests(string repositoryName);
    IEnumerable<GitHubPullRequest> GetPullRequestsByRelease(string repositoryName, string releaseLabel);
    IEnumerable<GitHubPullRequest> GetPullRequestsByLabelPattern(string repositoryName, string labelPattern);
    Dictionary<string, int> GetFirstTimeContributorPrNumbers(string repositoryName);

    // Issues
    GitHubSyncResult UpsertIssues(IEnumerable<GitHubIssue> issues);
    IEnumerable<GitHubIssue> GetAllIssues(string repositoryName);
    IEnumerable<GitHubIssue> GetIssuesByRelease(string repositoryName, string releaseLabel);
    IEnumerable<GitHubIssue> GetIssuesByLabelPattern(string repositoryName, string labelPattern);

    // Discussions
    GitHubSyncResult UpsertDiscussions(IEnumerable<GitHubDiscussion> discussions);
    IEnumerable<GitHubDiscussion> GetAllDiscussions(string repositoryName);
    IEnumerable<GitHubDiscussion> GetDiscussionsByCategory(string repositoryName, string category);

    // HQ Members
    GitHubSyncResult UpsertHqMembers(IEnumerable<GitHubHqMember> hqMembers);
    IEnumerable<GitHubHqMember> GetAllHqMembers();
    IEnumerable<GitHubHqMember> GetHqMembers();
    GitHubHqMember? GetHqMemberByLogin(string login);
    bool DeleteHqMemberByLogin(string login);
    bool IsHqMemberAtTime(string login, DateTime time);

    // NuGet Package Versions
    GitHubSyncResult UpsertNuGetPackageVersions(string packageId, Dictionary<string, DateTime> versions);
    Dictionary<string, DateTime> GetNuGetPackageVersions(string packageId);

    // Data Management
    void ClearAllData();
    void ClearGitHubData();
}

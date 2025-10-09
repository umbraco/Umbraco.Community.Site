using LiteDB;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubDataStore : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<GitHubPullRequest> _pullRequests;
    private readonly ILiteCollection<GitHubIssue> _issues;
    private readonly ILiteCollection<GitHubHqMember> _hqMembers;
    private readonly ILiteCollection<GitHubDiscussion> _discussions;

    public GitHubDataStore(IOptions<GitHubSyncOptions> options)
    {
        var databasePath = options.Value.DatabasePath;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _database = new LiteDatabase(databasePath);
        _pullRequests = _database.GetCollection<GitHubPullRequest>("pull_requests");
        _issues = _database.GetCollection<GitHubIssue>("issues");
        _hqMembers = _database.GetCollection<GitHubHqMember>("hq_members");
        _discussions = _database.GetCollection<GitHubDiscussion>("discussions");

        // Create indexes for better query performance
        _pullRequests.EnsureIndex(x => x.Repository.Name);
        _pullRequests.EnsureIndex(x => x.Number);
        _pullRequests.EnsureIndex(x => x.CreatedAt);
        _pullRequests.EnsureIndex(x => x.State);

        _issues.EnsureIndex(x => x.Repository.Name);
        _issues.EnsureIndex(x => x.Number);
        _issues.EnsureIndex(x => x.CreatedAt);
        _issues.EnsureIndex(x => x.State);

        _hqMembers.EnsureIndex(x => x.Login);

        _discussions.EnsureIndex(x => x.Repository.Name);
        _discussions.EnsureIndex(x => x.Number);
        _discussions.EnsureIndex(x => x.CategoryName);
    }

    public GitHubSyncResult UpsertPullRequests(IEnumerable<GitHubPullRequest> pullRequests)
    {
        var added = 0;
        var updated = 0;

        foreach (var pr in pullRequests)
        {
            var existing = _pullRequests.FindOne(x =>
                x.Repository.Name == pr.Repository.Name && x.Number == pr.Number);

            if (existing != null)
            {
                pr.Id = existing.Id;
                _pullRequests.Update(pr);
                updated++;
            }
            else
            {
                _pullRequests.Insert(pr);
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public GitHubSyncResult UpsertIssues(IEnumerable<GitHubIssue> issues)
    {
        var added = 0;
        var updated = 0;

        foreach (var issue in issues)
        {
            var existing = _issues.FindOne(x =>
                x.Repository.Name == issue.Repository.Name && x.Number == issue.Number);

            if (existing != null)
            {
                issue.Id = existing.Id;
                _issues.Update(issue);
                updated++;
            }
            else
            {
                _issues.Insert(issue);
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public IEnumerable<GitHubPullRequest> GetPullRequests(string? repositoryName = null)
    {
        if (string.IsNullOrEmpty(repositoryName))
        {
            return _pullRequests.FindAll().OrderByDescending(x => x.CreatedAt);
        }

        return _pullRequests.Find(x => x.Repository.Name == repositoryName)
            .OrderByDescending(x => x.CreatedAt);
    }

    public IEnumerable<GitHubIssue> GetIssues(string? repositoryName = null)
    {
        if (string.IsNullOrEmpty(repositoryName))
        {
            return _issues.FindAll().OrderByDescending(x => x.CreatedAt);
        }

        return _issues.Find(x => x.Repository.Name == repositoryName)
            .OrderByDescending(x => x.CreatedAt);
    }

    public (int PullRequests, int Issues) GetCounts(string? repositoryName = null)
    {
        int prCount;
        int issueCount;

        if (string.IsNullOrEmpty(repositoryName))
        {
            prCount = _pullRequests.Count();
            issueCount = _issues.Count();
        }
        else
        {
            prCount = _pullRequests.Count(x => x.Repository.Name == repositoryName);
            issueCount = _issues.Count(x => x.Repository.Name == repositoryName);
        }

        return (prCount, issueCount);
    }

    public GitHubSyncResult UpsertHqMembers(IEnumerable<GitHubHqMember> hqMembers)
    {
        var added = 0;
        var updated = 0;

        foreach (var member in hqMembers)
        {
            var existing = _hqMembers.FindOne(x => x.Login == member.Login);

            if (existing != null)
            {
                member.Id = existing.Id;
                _hqMembers.Update(member);
                updated++;
            }
            else
            {
                _hqMembers.Insert(member);
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public IEnumerable<GitHubHqMember> GetHqMembers()
    {
        return _hqMembers.FindAll().OrderBy(x => x.Login);
    }

    public int GetHqMembersCount()
    {
        return _hqMembers.Count();
    }

    public bool IsHqMemberAtTime(string login, DateTime date)
    {
        var member = _hqMembers.FindOne(x => x.Login == login);
        if (member == null) return false;

        return member.Periods.Any(p =>
            (p.Start == null || date >= p.Start) &&
            (p.End == null || date <= p.End));
    }

    public bool DeleteHqMember(int id)
    {
        return _hqMembers.Delete(id);
    }

    public IEnumerable<GitHubPullRequest> GetPullRequestsByLabelPattern(string repositoryName, string labelPattern)
    {
        return _pullRequests.Find(x =>
            x.Repository.Name == repositoryName &&
            x.Labels.Any(l => l.StartsWith(labelPattern)))
            .OrderByDescending(x => x.CreatedAt);
    }

    public IEnumerable<GitHubIssue> GetIssuesByLabelPattern(string repositoryName, string labelPattern)
    {
        return _issues.Find(x =>
            x.Repository.Name == repositoryName &&
            x.Labels.Any(l => l.StartsWith(labelPattern)))
            .OrderByDescending(x => x.CreatedAt);
    }

    public bool IsFirstTimeContributor(string repositoryName, string authorLogin, DateTime prCreatedAt)
    {
        // Find all PRs by this author in this repository before this PR
        var priorPrs = _pullRequests.Find(x =>
            x.Repository.Name == repositoryName &&
            x.Author != null &&
            x.Author.Login == authorLogin &&
            x.CreatedAt < prCreatedAt);

        return !priorPrs.Any();
    }

    public Dictionary<string, int> GetFirstTimeContributorPrNumbers(string repositoryName)
    {
        // Get all merged PRs for this repository ordered by merge date
        var allMergedRepoPrs = _pullRequests.Find(x =>
                x.Repository.Name == repositoryName &&
                x.MergedAt != null)
            .Where(x => x.Author != null)
            .OrderBy(x => x.MergedAt)
            .ToList();

        // Build a dictionary of each author's first merged PR number
        var firstMergedPrNumbers = new Dictionary<string, int>();
        foreach (var pr in allMergedRepoPrs)
        {
            if (pr.Author != null && pr.MergedAt.HasValue && !firstMergedPrNumbers.ContainsKey(pr.Author.Login))
            {
                firstMergedPrNumbers[pr.Author.Login] = pr.Number;
            }
        }

        return firstMergedPrNumbers;
    }

    public HashSet<string> GetFirstTimeContributorLogins(string repositoryName, IEnumerable<GitHubPullRequest> prs)
    {
        var prList = prs.ToList();
        var firstMergedPrNumbers = GetFirstTimeContributorPrNumbers(repositoryName);

        // Check which authors in our list have their first-time contribution PR included
        var firstTimeContributors = new HashSet<string>();
        foreach (var pr in prList)
        {
            if (pr.Author != null &&
                firstMergedPrNumbers.TryGetValue(pr.Author.Login, out var firstPrNumber) &&
                pr.Number == firstPrNumber)
            {
                firstTimeContributors.Add(pr.Author.Login);
            }
        }

        return firstTimeContributors;
    }

    public GitHubSyncResult UpsertDiscussions(IEnumerable<GitHubDiscussion> discussions)
    {
        var added = 0;
        var updated = 0;

        foreach (var discussion in discussions)
        {
            var existing = _discussions.FindOne(x =>
                x.Repository.Name == discussion.Repository.Name && x.Number == discussion.Number);

            if (existing != null)
            {
                discussion.Id = existing.Id;
                _discussions.Update(discussion);
                updated++;
            }
            else
            {
                _discussions.Insert(discussion);
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public IEnumerable<GitHubDiscussion> GetDiscussionsByCategory(string repositoryName, string categoryName)
    {
        return _discussions.Find(x =>
            x.Repository.Name == repositoryName &&
            x.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAt);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}

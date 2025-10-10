using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubCosmosDbStore : IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _pullRequestsContainer;
    private readonly Container _issuesContainer;
    private readonly Container _hqMembersContainer;
    private readonly Container _discussionsContainer;

    public GitHubCosmosDbStore(IOptions<GitHubSyncOptions> options)
    {
        var endpoint = options.Value.CosmosDbEndpoint;
        var masterKey = options.Value.CosmosDbMasterKey;
        var databaseName = options.Value.CosmosDatabaseName;

        _cosmosClient = new CosmosClient(endpoint, masterKey);
        var database = _cosmosClient.GetDatabase(databaseName);

        _pullRequestsContainer = database.GetContainer("PullRequests");
        _issuesContainer = database.GetContainer("Issues");
        _hqMembersContainer = database.GetContainer("HqMembers");
        _discussionsContainer = database.GetContainer("Discussions");
    }

    public GitHubSyncResult UpsertPullRequests(IEnumerable<GitHubPullRequest> pullRequests)
    {
        return UpsertPullRequestsAsync(pullRequests).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertPullRequestsAsync(IEnumerable<GitHubPullRequest> pullRequests)
    {
        var added = 0;
        var updated = 0;

        foreach (var pr in pullRequests)
        {
            // Generate ID if empty
            if (string.IsNullOrEmpty(pr.Id))
            {
                pr.Id = $"{pr.Repository.Name}-pr-{pr.Number}";
            }

            try
            {
                var existing = await _pullRequestsContainer.ReadItemAsync<GitHubPullRequest>(
                    pr.Id,
                    new PartitionKey(pr.Repository.Name));

                await _pullRequestsContainer.ReplaceItemAsync(pr, pr.Id, new PartitionKey(pr.Repository.Name));
                updated++;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await _pullRequestsContainer.CreateItemAsync(pr, new PartitionKey(pr.Repository.Name));
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public GitHubSyncResult UpsertIssues(IEnumerable<GitHubIssue> issues)
    {
        return UpsertIssuesAsync(issues).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertIssuesAsync(IEnumerable<GitHubIssue> issues)
    {
        var added = 0;
        var updated = 0;

        foreach (var issue in issues)
        {
            // Generate ID if empty
            if (string.IsNullOrEmpty(issue.Id))
            {
                issue.Id = $"{issue.Repository.Name}-issue-{issue.Number}";
            }

            try
            {
                var existing = await _issuesContainer.ReadItemAsync<GitHubIssue>(
                    issue.Id,
                    new PartitionKey(issue.Repository.Name));

                await _issuesContainer.ReplaceItemAsync(issue, issue.Id, new PartitionKey(issue.Repository.Name));
                updated++;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await _issuesContainer.CreateItemAsync(issue, new PartitionKey(issue.Repository.Name));
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public IEnumerable<GitHubPullRequest> GetPullRequests(string? repositoryName = null)
    {
        if (string.IsNullOrEmpty(repositoryName))
        {
            // Cross-partition query for all repositories
            var queryDefinition = new QueryDefinition("SELECT * FROM c");
            var iterator = _pullRequestsContainer.GetItemQueryIterator<GitHubPullRequest>(queryDefinition);

            var results = new List<GitHubPullRequest>();
            while (iterator.HasMoreResults)
            {
                var response = iterator.ReadNextAsync().GetAwaiter().GetResult();
                results.AddRange(response);
            }

            return results.OrderByDescending(x => x.CreatedAt);
        }

        // Single partition query
        var query = new QueryDefinition("SELECT * FROM c WHERE c.Repository.Name = @repoName")
            .WithParameter("@repoName", repositoryName);

        var queryIterator = _pullRequestsContainer.GetItemQueryIterator<GitHubPullRequest>(query);
        var prs = new List<GitHubPullRequest>();

        while (queryIterator.HasMoreResults)
        {
            var response = queryIterator.ReadNextAsync().GetAwaiter().GetResult();
            prs.AddRange(response);
        }

        return prs.OrderByDescending(x => x.CreatedAt);
    }

    public IEnumerable<GitHubIssue> GetIssues(string? repositoryName = null)
    {
        if (string.IsNullOrEmpty(repositoryName))
        {
            // Cross-partition query for all repositories
            var queryDefinition = new QueryDefinition("SELECT * FROM c");
            var iterator = _issuesContainer.GetItemQueryIterator<GitHubIssue>(queryDefinition);

            var results = new List<GitHubIssue>();
            while (iterator.HasMoreResults)
            {
                var response = iterator.ReadNextAsync().GetAwaiter().GetResult();
                results.AddRange(response);
            }

            return results.OrderByDescending(x => x.CreatedAt);
        }

        // Single partition query
        var query = new QueryDefinition("SELECT * FROM c WHERE c.Repository.Name = @repoName")
            .WithParameter("@repoName", repositoryName);

        var queryIterator = _issuesContainer.GetItemQueryIterator<GitHubIssue>(query);
        var issues = new List<GitHubIssue>();

        while (queryIterator.HasMoreResults)
        {
            var response = queryIterator.ReadNextAsync().GetAwaiter().GetResult();
            issues.AddRange(response);
        }

        return issues.OrderByDescending(x => x.CreatedAt);
    }

    public (int PullRequests, int Issues) GetCounts(string? repositoryName = null)
    {
        int prCount;
        int issueCount;

        if (string.IsNullOrEmpty(repositoryName))
        {
            // Cross-partition count
            var prQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            var prIterator = _pullRequestsContainer.GetItemQueryIterator<int>(prQuery);
            prCount = prIterator.ReadNextAsync().GetAwaiter().GetResult().FirstOrDefault();

            var issueQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            var issueIterator = _issuesContainer.GetItemQueryIterator<int>(issueQuery);
            issueCount = issueIterator.ReadNextAsync().GetAwaiter().GetResult().FirstOrDefault();
        }
        else
        {
            // Single partition count
            var prQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.Repository.Name = @repoName")
                .WithParameter("@repoName", repositoryName);
            var prIterator = _pullRequestsContainer.GetItemQueryIterator<int>(prQuery);
            prCount = prIterator.ReadNextAsync().GetAwaiter().GetResult().FirstOrDefault();

            var issueQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.Repository.Name = @repoName")
                .WithParameter("@repoName", repositoryName);
            var issueIterator = _issuesContainer.GetItemQueryIterator<int>(issueQuery);
            issueCount = issueIterator.ReadNextAsync().GetAwaiter().GetResult().FirstOrDefault();
        }

        return (prCount, issueCount);
    }

    public GitHubSyncResult UpsertHqMembers(IEnumerable<GitHubHqMember> hqMembers)
    {
        return UpsertHqMembersAsync(hqMembers).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertHqMembersAsync(IEnumerable<GitHubHqMember> hqMembers)
    {
        var added = 0;
        var updated = 0;

        foreach (var member in hqMembers)
        {
            // Use Login as ID if empty
            if (string.IsNullOrEmpty(member.Id))
            {
                member.Id = member.Login;
            }

            try
            {
                var existing = await _hqMembersContainer.ReadItemAsync<GitHubHqMember>(
                    member.Id,
                    new PartitionKey(member.Login));

                await _hqMembersContainer.ReplaceItemAsync(member, member.Id, new PartitionKey(member.Login));
                updated++;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await _hqMembersContainer.CreateItemAsync(member, new PartitionKey(member.Login));
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public IEnumerable<GitHubHqMember> GetHqMembers()
    {
        var queryDefinition = new QueryDefinition("SELECT * FROM c");
        var iterator = _hqMembersContainer.GetItemQueryIterator<GitHubHqMember>(queryDefinition);

        var results = new List<GitHubHqMember>();
        while (iterator.HasMoreResults)
        {
            var response = iterator.ReadNextAsync().GetAwaiter().GetResult();
            results.AddRange(response);
        }

        return results.OrderBy(x => x.Login);
    }

    public int GetHqMembersCount()
    {
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        var iterator = _hqMembersContainer.GetItemQueryIterator<int>(query);
        return iterator.ReadNextAsync().GetAwaiter().GetResult().FirstOrDefault();
    }

    public bool IsHqMemberAtTime(string login, DateTime date)
    {
        try
        {
            var member = _hqMembersContainer.ReadItemAsync<GitHubHqMember>(
                login,
                new PartitionKey(login)).GetAwaiter().GetResult().Resource;

            if (member == null) return false;

            return member.Periods.Any(p =>
                (p.Start == null || date >= p.Start) &&
                (p.End == null || date <= p.End));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public bool DeleteHqMember(int id)
    {
        // Since ID is now string (Login), we need to find by the old int ID
        // This method signature is problematic - keeping for compatibility but it won't work well
        // Better to change signature to DeleteHqMember(string login)
        return false;
    }

    public bool DeleteHqMember(string login)
    {
        try
        {
            _hqMembersContainer.DeleteItemAsync<GitHubHqMember>(
                login,
                new PartitionKey(login)).GetAwaiter().GetResult();
            return true;
        }
        catch (CosmosException)
        {
            return false;
        }
    }

    public IEnumerable<GitHubPullRequest> GetPullRequestsByLabelPattern(string repositoryName, string labelPattern)
    {
        // Get all PRs for this repository, then filter in-memory for label pattern
        var query = new QueryDefinition("SELECT * FROM c WHERE c.Repository.Name = @repoName")
            .WithParameter("@repoName", repositoryName);

        var queryIterator = _pullRequestsContainer.GetItemQueryIterator<GitHubPullRequest>(query);
        var prs = new List<GitHubPullRequest>();

        while (queryIterator.HasMoreResults)
        {
            var response = queryIterator.ReadNextAsync().GetAwaiter().GetResult();
            // Filter in-memory for StartsWith since Cosmos doesn't support it directly
            prs.AddRange(response.Where(x => x.Labels.Any(l => l.StartsWith(labelPattern, StringComparison.OrdinalIgnoreCase))));
        }

        return prs.OrderByDescending(x => x.CreatedAt);
    }

    public IEnumerable<GitHubIssue> GetIssuesByLabelPattern(string repositoryName, string labelPattern)
    {
        // Get all issues for this repository, then filter in-memory for label pattern
        var query = new QueryDefinition("SELECT * FROM c WHERE c.Repository.Name = @repoName")
            .WithParameter("@repoName", repositoryName);

        var queryIterator = _issuesContainer.GetItemQueryIterator<GitHubIssue>(query);
        var issues = new List<GitHubIssue>();

        while (queryIterator.HasMoreResults)
        {
            var response = queryIterator.ReadNextAsync().GetAwaiter().GetResult();
            // Filter in-memory for StartsWith since Cosmos doesn't support it directly
            issues.AddRange(response.Where(x => x.Labels.Any(l => l.StartsWith(labelPattern, StringComparison.OrdinalIgnoreCase))));
        }

        return issues.OrderByDescending(x => x.CreatedAt);
    }

    public bool IsFirstTimeContributor(string repositoryName, string authorLogin, DateTime prCreatedAt)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.Repository.Name = @repoName AND c.Author.Login = @authorLogin AND c.CreatedAt < @createdAt")
            .WithParameter("@repoName", repositoryName)
            .WithParameter("@authorLogin", authorLogin)
            .WithParameter("@createdAt", prCreatedAt);

        var queryIterator = _pullRequestsContainer.GetItemQueryIterator<GitHubPullRequest>(query);
        var priorPrs = new List<GitHubPullRequest>();

        while (queryIterator.HasMoreResults)
        {
            var response = queryIterator.ReadNextAsync().GetAwaiter().GetResult();
            priorPrs.AddRange(response);
        }

        return !priorPrs.Any();
    }

    public Dictionary<string, int> GetFirstTimeContributorPrNumbers(string repositoryName)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.Repository.Name = @repoName AND IS_DEFINED(c.MergedAt)")
            .WithParameter("@repoName", repositoryName);

        var queryIterator = _pullRequestsContainer.GetItemQueryIterator<GitHubPullRequest>(query);
        var allMergedRepoPrs = new List<GitHubPullRequest>();

        while (queryIterator.HasMoreResults)
        {
            var response = queryIterator.ReadNextAsync().GetAwaiter().GetResult();
            allMergedRepoPrs.AddRange(response);
        }

        var orderedPrs = allMergedRepoPrs
            .Where(x => x.Author != null)
            .OrderBy(x => x.MergedAt)
            .ToList();

        var firstMergedPrNumbers = new Dictionary<string, int>();
        foreach (var pr in orderedPrs)
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
        return UpsertDiscussionsAsync(discussions).GetAwaiter().GetResult();
    }

    private async Task<GitHubSyncResult> UpsertDiscussionsAsync(IEnumerable<GitHubDiscussion> discussions)
    {
        var added = 0;
        var updated = 0;

        foreach (var discussion in discussions)
        {
            // Generate ID if empty
            if (string.IsNullOrEmpty(discussion.Id))
            {
                discussion.Id = $"{discussion.Repository.Name}-discussion-{discussion.Number}";
            }

            try
            {
                var existing = await _discussionsContainer.ReadItemAsync<GitHubDiscussion>(
                    discussion.Id,
                    new PartitionKey(discussion.Repository.Name));

                await _discussionsContainer.ReplaceItemAsync(discussion, discussion.Id, new PartitionKey(discussion.Repository.Name));
                updated++;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await _discussionsContainer.CreateItemAsync(discussion, new PartitionKey(discussion.Repository.Name));
                added++;
            }
        }

        return new GitHubSyncResult { Added = added, Updated = updated };
    }

    public IEnumerable<GitHubDiscussion> GetDiscussionsByCategory(string repositoryName, string categoryName)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.Repository.Name = @repoName AND LOWER(c.CategoryName) = LOWER(@categoryName)")
            .WithParameter("@repoName", repositoryName)
            .WithParameter("@categoryName", categoryName);

        var queryIterator = _discussionsContainer.GetItemQueryIterator<GitHubDiscussion>(query);
        var discussions = new List<GitHubDiscussion>();

        while (queryIterator.HasMoreResults)
        {
            var response = queryIterator.ReadNextAsync().GetAwaiter().GetResult();
            discussions.AddRange(response);
        }

        return discussions.OrderByDescending(x => x.CreatedAt);
    }

    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }
}

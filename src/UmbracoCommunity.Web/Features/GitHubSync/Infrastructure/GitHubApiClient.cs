using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubApiClient
{
    private readonly HttpClient _httpClient;
    private readonly GitHubSyncOptions _options;
    private readonly ILogger<GitHubApiClient> _logger;
    private const string GitHubGraphQLEndpoint = "https://api.github.com/graphql";
    private RateLimitInfo? _lastRateLimitInfo;

    public GitHubApiClient(IHttpClientFactory httpClientFactory, IOptions<GitHubSyncOptions> options, ILogger<GitHubApiClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UmbracoCommunity-GitHubSync", "1.0"));
    }

    public async Task<RateLimitInfo> GetRateLimitInfoAsync(Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        var query = @"
            query {
              rateLimit {
                limit
                remaining
                resetAt
                used
              }
            }";

        var response = await ExecuteGraphQLAsync<RateLimitResponse>(query, new { }, cancellationToken);

        if (response?.Data?.RateLimit != null)
        {
            var info = new RateLimitInfo
            {
                Limit = response.Data.RateLimit.Limit,
                Remaining = response.Data.RateLimit.Remaining,
                Used = response.Data.RateLimit.Used,
                ResetAt = DateTime.Parse(response.Data.RateLimit.ResetAt)
            };

            _lastRateLimitInfo = info;

            var message = $"GitHub API Rate Limit - Used: {info.Used}/{info.Limit}, Remaining: {info.Remaining}, Resets at: {info.ResetAt:yyyy-MM-dd HH:mm:ss UTC}";
            _logger.LogInformation(message);
            log?.Invoke(message);

            return info;
        }

        throw new InvalidOperationException("Failed to fetch rate limit information");
    }

    public async Task<List<(string Login, string Name)>> FetchOrganizationTeamMembersAsync(
        List<string> teamSlugs,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var message = $"Fetching team members from {_options.Organization} for {teamSlugs.Count} teams...";
        _logger.LogInformation(message);
        log?.Invoke(message);

        var allMembers = new HashSet<(string Login, string Name)>();

        foreach (var teamSlug in teamSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            log?.Invoke($"Fetching members for team: {teamSlug}");

            string? cursor = null;
            bool hasNextPage = true;

            while (hasNextPage && !cancellationToken.IsCancellationRequested)
            {
                var query = @"
                    query($org: String!, $teamSlug: String!, $cursor: String) {
                      organization(login: $org) {
                        team(slug: $teamSlug) {
                          members(first: 100, after: $cursor) {
                            nodes {
                              login
                              name
                            }
                            pageInfo {
                              hasNextPage
                              endCursor
                            }
                          }
                        }
                      }
                    }";

                var variables = new
                {
                    org = _options.Organization,
                    teamSlug,
                    cursor
                };

                var response = await ExecuteGraphQLAsync<TeamMembersResponse>(query, variables, cancellationToken);

                if (response?.Data?.Organization?.Team?.Members != null)
                {
                    foreach (var member in response.Data.Organization.Team.Members.Nodes)
                    {
                        allMembers.Add((member.Login, member.Name ?? member.Login));
                    }

                    hasNextPage = response.Data.Organization.Team.Members.PageInfo.HasNextPage;
                    cursor = response.Data.Organization.Team.Members.PageInfo.EndCursor;
                }
                else
                {
                    _logger.LogWarning("Team {TeamSlug} not found or inaccessible", teamSlug);
                    break;
                }
            }
        }

        var finalMessage = $"Found {allMembers.Count} unique members across all HQ teams";
        _logger.LogInformation(finalMessage);
        log?.Invoke(finalMessage);
        return allMembers.ToList();
    }

    public async Task<List<string>> FetchAllRepositoriesAsync(Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        var message = $"Fetching repositories from {_options.Organization}...";
        _logger.LogInformation(message);
        log?.Invoke(message);

        var repositories = new List<string>();
        string? cursor = null;
        bool hasNextPage = true;

        while (hasNextPage && !cancellationToken.IsCancellationRequested)
        {
            var query = @"
                query($org: String!, $cursor: String) {
                  organization(login: $org) {
                    repositories(first: 100, after: $cursor, orderBy: {field: PUSHED_AT, direction: DESC}) {
                      nodes {
                        name
                        isPrivate
                      }
                      pageInfo {
                        hasNextPage
                        endCursor
                      }
                    }
                  }
                }";

            var variables = new
            {
                org = _options.Organization,
                cursor
            };

            var response = await ExecuteGraphQLAsync<RepositoriesResponse>(query, variables, cancellationToken);

            if (response?.Data?.Organization?.Repositories != null)
            {
                foreach (var repo in response.Data.Organization.Repositories.Nodes)
                {
                    if (!repo.IsPrivate)
                    {
                        repositories.Add(repo.Name);
                    }
                }

                hasNextPage = response.Data.Organization.Repositories.PageInfo.HasNextPage;
                cursor = response.Data.Organization.Repositories.PageInfo.EndCursor;
            }
            else
            {
                break;
            }
        }

        var finalMessage = $"Found {repositories.Count} total public repositories";
        _logger.LogInformation(finalMessage);
        log?.Invoke(finalMessage);
        return repositories;
    }

    public async Task<List<GitHubPullRequest>> FetchAllPullRequestsAsync(
        string? specificRepo = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var repositories = specificRepo != null
            ? new List<string> { specificRepo }
            : _options.Repositories.Count > 0
                ? _options.Repositories
                : await FetchAllRepositoriesAsync(log, cancellationToken);

        var allPRs = new List<GitHubPullRequest>();
        var repoIndex = 0;

        foreach (var repoName in repositories)
        {
            if (cancellationToken.IsCancellationRequested) break;

            repoIndex++;
            if (repositories.Count > 1)
            {
                log?.Invoke($"Processing repository {repoIndex}/{repositories.Count}: {repoName}");
                await GetRateLimitInfoAsync(log, cancellationToken);
            }

            var prs = await FetchPullRequestsForRepositoryAsync(repoName, null, log, cancellationToken);
            allPRs.AddRange(prs);
        }

        return allPRs.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<List<GitHubIssue>> FetchAllIssuesAsync(
        string? specificRepo = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var repositories = specificRepo != null
            ? new List<string> { specificRepo }
            : _options.Repositories.Count > 0
                ? _options.Repositories
                : await FetchAllRepositoriesAsync(log, cancellationToken);

        var allIssues = new List<GitHubIssue>();
        var repoIndex = 0;

        foreach (var repoName in repositories)
        {
            if (cancellationToken.IsCancellationRequested) break;

            repoIndex++;
            if (repositories.Count > 1)
            {
                log?.Invoke($"Processing repository {repoIndex}/{repositories.Count}: {repoName}");
                await GetRateLimitInfoAsync(log, cancellationToken);
            }

            var issues = await FetchIssuesForRepositoryAsync(repoName, null, log, cancellationToken);
            allIssues.AddRange(issues);
        }

        return allIssues.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<List<GitHubPullRequest>> FetchRecentPullRequestsAsync(
        string? specificRepo = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-_options.RecentDays);
        var repositories = specificRepo != null
            ? new List<string> { specificRepo }
            : _options.Repositories.Count > 0
                ? _options.Repositories
                : await FetchAllRepositoriesAsync(log, cancellationToken);

        var allPRs = new List<GitHubPullRequest>();
        var repoIndex = 0;

        foreach (var repoName in repositories)
        {
            if (cancellationToken.IsCancellationRequested) break;

            repoIndex++;
            if (repositories.Count > 1)
            {
                log?.Invoke($"Processing repository {repoIndex}/{repositories.Count}: {repoName}");
                await GetRateLimitInfoAsync(log, cancellationToken);
            }

            var prs = await FetchPullRequestsForRepositoryAsync(repoName, cutoffDate, log, cancellationToken);
            allPRs.AddRange(prs);
        }

        return allPRs.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToList();
    }

    public async Task<List<GitHubIssue>> FetchRecentIssuesAsync(
        string? specificRepo = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-_options.RecentDays);
        var repositories = specificRepo != null
            ? new List<string> { specificRepo }
            : _options.Repositories.Count > 0
                ? _options.Repositories
                : await FetchAllRepositoriesAsync(log, cancellationToken);

        var allIssues = new List<GitHubIssue>();
        var repoIndex = 0;

        foreach (var repoName in repositories)
        {
            if (cancellationToken.IsCancellationRequested) break;

            repoIndex++;
            if (repositories.Count > 1)
            {
                log?.Invoke($"Processing repository {repoIndex}/{repositories.Count}: {repoName}");
                await GetRateLimitInfoAsync(log, cancellationToken);
            }

            var issues = await FetchIssuesForRepositoryAsync(repoName, cutoffDate, log, cancellationToken);
            allIssues.AddRange(issues);
        }

        return allIssues.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToList();
    }

    private async Task<List<GitHubPullRequest>> FetchPullRequestsForRepositoryAsync(
        string repoName,
        DateTime? updatedAfter = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var message = $"Fetching PRs for {repoName}...";
        _logger.LogInformation(message);
        log?.Invoke(message);
        var prs = new List<GitHubPullRequest>();
        string? cursor = null;
        bool hasNextPage = true;
        int pageCount = 0;

        while (hasNextPage && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                pageCount++;

                // Log rate limit every 5 pages
                if (pageCount % 5 == 0)
                {
                    log?.Invoke($"Fetched {pageCount} pages so far ({prs.Count} PRs)...");
                    await GetRateLimitInfoAsync(log, cancellationToken);
                }

                var query = @"
                    query($org: String!, $repo: String!, $cursor: String) {
                      organization(login: $org) {
                        repository(name: $repo) {
                          pullRequests(first: 100, after: $cursor, states: [OPEN, MERGED], orderBy: {field: UPDATED_AT, direction: DESC}) {
                            nodes {
                              title
                              number
                              url
                              createdAt
                              updatedAt
                              mergedAt
                              state
                              author {
                                login
                                url
                                ... on User {
                                  name
                                }
                              }
                              mergedBy {
                                login
                                url
                                ... on User {
                                  name
                                }
                              }
                              labels(first: 100) {
                                nodes {
                                  name
                                }
                              }
                            }
                            pageInfo {
                              hasNextPage
                              endCursor
                            }
                          }
                        }
                      }
                    }";

                var variables = new
                {
                    org = _options.Organization,
                    repo = repoName,
                    cursor
                };

                var response = await ExecuteGraphQLAsync<PullRequestsResponse>(query, variables, cancellationToken);

                if (response?.Data?.Organization?.Repository?.PullRequests != null)
                {
                    log?.Invoke($"Page {pageCount}: Received {response.Data.Organization.Repository.PullRequests.Nodes.Count} PRs");
                    foreach (var pr in response.Data.Organization.Repository.PullRequests.Nodes)
                    {
                        var updatedAt = DateTime.Parse(pr.UpdatedAt);

                        // Check if updated after cutoff date
                        if (updatedAfter.HasValue && updatedAt < updatedAfter.Value)
                        {
                            hasNextPage = false;
                            break;
                        }

                        // Filter out dependabot
                        if (pr.Author != null && !pr.Author.Login.Contains("dependabot", StringComparison.OrdinalIgnoreCase))
                        {
                            prs.Add(new GitHubPullRequest
                            {
                                Id = $"{repoName}#{pr.Number}",
                                Title = pr.Title,
                                Number = pr.Number,
                                Url = pr.Url,
                                CreatedAt = DateTime.Parse(pr.CreatedAt),
                                UpdatedAt = updatedAt,
                                MergedAt = !string.IsNullOrEmpty(pr.MergedAt) ? DateTime.Parse(pr.MergedAt) : null,
                                State = pr.State,
                                Author = new GitHubAuthor
                                {
                                    Login = pr.Author.Login,
                                    Name = pr.Author.Name,
                                    Url = pr.Author.Url
                                },
                                MergedBy = pr.MergedBy != null ? new GitHubAuthor
                                {
                                    Login = pr.MergedBy.Login,
                                    Name = pr.MergedBy.Name,
                                    Url = pr.MergedBy.Url
                                } : null,
                                Repository = new GitHubRepository
                                {
                                    Name = repoName,
                                    Url = $"https://github.com/{_options.Organization}/{repoName}"
                                },
                                Labels = pr.Labels?.Nodes?.Select(l => l.Name).ToList() ?? new List<string>()
                            });
                        }
                    }

                    hasNextPage = hasNextPage && response.Data.Organization.Repository.PullRequests.PageInfo.HasNextPage;
                    cursor = response.Data.Organization.Repository.PullRequests.PageInfo.EndCursor;
                }
                else
                {
                    var errorMessage = $"Repository {repoName} not found or inaccessible. Response structure issue.";
                    _logger.LogWarning(errorMessage);
                    log?.Invoke(errorMessage);

                    if (response?.Data?.Organization == null)
                    {
                        log?.Invoke("Organization data is null");
                    }
                    else if (response?.Data?.Organization?.Repository == null)
                    {
                        log?.Invoke("Repository data is null");
                    }
                    else if (response?.Data?.Organization?.Repository?.PullRequests == null)
                    {
                        log?.Invoke("PullRequests data is null");
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error fetching pull requests for {repoName}: {ex.Message}";
                _logger.LogError(ex, "Error fetching pull requests for {RepoName}", repoName);
                log?.Invoke(errorMsg);
                break;
            }
        }

        var completedMessage = $"Completed {repoName}: found {prs.Count} PRs";
        _logger.LogInformation(completedMessage);
        log?.Invoke(completedMessage);
        return prs;
    }

    private async Task<List<GitHubIssue>> FetchIssuesForRepositoryAsync(
        string repoName,
        DateTime? updatedAfter = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var message = $"Fetching issues for {repoName}...";
        _logger.LogInformation(message);
        log?.Invoke(message);
        var issues = new List<GitHubIssue>();
        string? cursor = null;
        bool hasNextPage = true;
        int pageCount = 0;

        while (hasNextPage && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                pageCount++;

                // Log rate limit every 5 pages
                if (pageCount % 5 == 0)
                {
                    log?.Invoke($"Fetched {pageCount} pages so far ({issues.Count} issues)...");
                    await GetRateLimitInfoAsync(log, cancellationToken);
                }

                var query = @"
                    query($org: String!, $repo: String!, $cursor: String) {
                      organization(login: $org) {
                        repository(name: $repo) {
                          issues(first: 100, after: $cursor, states: [OPEN, CLOSED], orderBy: {field: UPDATED_AT, direction: DESC}) {
                            nodes {
                              title
                              number
                              url
                              createdAt
                              updatedAt
                              state
                              author {
                                login
                                url
                              }
                              labels(first: 100) {
                                nodes {
                                  name
                                }
                              }
                            }
                            pageInfo {
                              hasNextPage
                              endCursor
                            }
                          }
                        }
                      }
                    }";

                var variables = new
                {
                    org = _options.Organization,
                    repo = repoName,
                    cursor
                };

                var response = await ExecuteGraphQLAsync<IssuesResponse>(query, variables, cancellationToken);

                if (response?.Data?.Organization?.Repository?.Issues != null)
                {
                    foreach (var issue in response.Data.Organization.Repository.Issues.Nodes)
                    {
                        var updatedAt = DateTime.Parse(issue.UpdatedAt);

                        // Check if updated after cutoff date
                        if (updatedAfter.HasValue && updatedAt < updatedAfter.Value)
                        {
                            hasNextPage = false;
                            break;
                        }

                        // Filter out dependabot
                        if (issue.Author != null && !issue.Author.Login.Contains("dependabot", StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(new GitHubIssue
                            {
                                Id = $"{repoName}#{issue.Number}",
                                Title = issue.Title,
                                Number = issue.Number,
                                Url = issue.Url,
                                CreatedAt = DateTime.Parse(issue.CreatedAt),
                                UpdatedAt = updatedAt,
                                State = issue.State,
                                Author = new GitHubAuthor
                                {
                                    Login = issue.Author.Login,
                                    Url = issue.Author.Url
                                },
                                Repository = new GitHubRepository
                                {
                                    Name = repoName,
                                    Url = $"https://github.com/{_options.Organization}/{repoName}"
                                },
                                Labels = issue.Labels?.Nodes?.Select(l => l.Name).ToList() ?? new List<string>()
                            });
                        }
                    }

                    hasNextPage = hasNextPage && response.Data.Organization.Repository.Issues.PageInfo.HasNextPage;
                    cursor = response.Data.Organization.Repository.Issues.PageInfo.EndCursor;
                }
                else
                {
                    _logger.LogWarning("Repository {RepoName} not found or inaccessible", repoName);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching issues for {RepoName}", repoName);
                break;
            }
        }

        var completedMessage = $"Completed {repoName}: found {issues.Count} issues";
        _logger.LogInformation(completedMessage);
        log?.Invoke(completedMessage);
        return issues;
    }

    public async Task<List<GitHubDiscussion>> FetchDiscussionsByCategoryAsync(
        string repositoryName,
        string categoryName,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var message = $"Fetching discussions for {repositoryName} in category '{categoryName}'...";
        _logger.LogInformation(message);
        log?.Invoke(message);

        var discussions = new List<GitHubDiscussion>();
        string? cursor = null;
        bool hasNextPage = true;

        while (hasNextPage && !cancellationToken.IsCancellationRequested)
        {
            var query = @"
                query($org: String!, $repo: String!, $cursor: String) {
                  repository(owner: $org, name: $repo) {
                    discussions(first: 100, after: $cursor, orderBy: {field: CREATED_AT, direction: DESC}) {
                      nodes {
                        title
                        number
                        url
                        body
                        createdAt
                        updatedAt
                        category {
                          id
                          name
                        }
                        labels(first: 20) {
                          nodes {
                            name
                          }
                        }
                      }
                      pageInfo {
                        hasNextPage
                        endCursor
                      }
                    }
                  }
                }";

            var variables = new
            {
                org = _options.Organization,
                repo = repositoryName,
                cursor
            };

            var response = await ExecuteGraphQLAsync<DiscussionsResponse>(query, variables, cancellationToken);

            if (response?.Data?.Repository?.Discussions != null)
            {
                foreach (var discussion in response.Data.Repository.Discussions.Nodes)
                {
                    // Filter by category name
                    if (discussion.Category?.Name?.Equals(categoryName, StringComparison.OrdinalIgnoreCase) != true)
                        continue;

                    discussions.Add(new GitHubDiscussion
                    {
                        Id = $"{repositoryName}#{discussion.Number}",
                        Title = discussion.Title,
                        Number = discussion.Number,
                        Url = discussion.Url,
                        Body = discussion.Body,
                        CreatedAt = DateTime.Parse(discussion.CreatedAt),
                        UpdatedAt = string.IsNullOrEmpty(discussion.UpdatedAt) ? null : DateTime.Parse(discussion.UpdatedAt),
                        Repository = new GitHubRepository
                        {
                            Name = repositoryName,
                            Url = $"https://github.com/{_options.Organization}/{repositoryName}"
                        },
                        Labels = discussion.Labels?.Nodes?.Select(l => l.Name).ToList() ?? new List<string>(),
                        CategoryId = discussion.Category?.Id ?? string.Empty,
                        CategoryName = discussion.Category?.Name ?? string.Empty
                    });
                }

                hasNextPage = response.Data.Repository.Discussions.PageInfo.HasNextPage;
                cursor = response.Data.Repository.Discussions.PageInfo.EndCursor;
            }
            else
            {
                _logger.LogWarning("Repository {RepoName} not found or inaccessible", repositoryName);
                break;
            }
        }

        var completedMessage = $"Completed {repositoryName}: found {discussions.Count} discussions in category '{categoryName}'";
        _logger.LogInformation(completedMessage);
        log?.Invoke(completedMessage);
        return discussions;
    }

    private async Task<GraphQLResponse<T>?> ExecuteGraphQLAsync<T>(string query, object variables, CancellationToken cancellationToken)
    {
        var request = new
        {
            query,
            variables
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(GitHubGraphQLEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<GraphQLResponse<T>>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result?.Errors != null && result.Errors.Count > 0)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Message));
            _logger.LogError("GraphQL errors: {Errors}", errors);
            throw new InvalidOperationException($"GraphQL query failed: {errors}");
        }

        return result;
    }

    // Response models for GraphQL deserialization
    private class GraphQLResponse<T>
    {
        public T? Data { get; set; }
        public List<GraphQLError>? Errors { get; set; }
    }

    private class GraphQLError
    {
        public string Message { get; set; } = string.Empty;
    }

    private class RepositoriesResponse
    {
        public OrganizationData? Organization { get; set; }

        public class OrganizationData
        {
            public RepositoriesData? Repositories { get; set; }
        }

        public class RepositoriesData
        {
            public List<RepositoryNode> Nodes { get; set; } = new();
            public PageInfo PageInfo { get; set; } = new();
        }

        public class RepositoryNode
        {
            public string Name { get; set; } = string.Empty;
            public bool IsPrivate { get; set; }
        }
    }

    private class PullRequestsResponse
    {
        public OrganizationData? Organization { get; set; }

        public class OrganizationData
        {
            public RepositoryData? Repository { get; set; }
        }

        public class RepositoryData
        {
            public PullRequestsData? PullRequests { get; set; }
        }

        public class PullRequestsData
        {
            public List<PullRequestNode> Nodes { get; set; } = new();
            public PageInfo PageInfo { get; set; } = new();
        }

        public class PullRequestNode
        {
            public string Title { get; set; } = string.Empty;
            public int Number { get; set; }
            public string Url { get; set; } = string.Empty;
            public string CreatedAt { get; set; } = string.Empty;
            public string UpdatedAt { get; set; } = string.Empty;
            public string? MergedAt { get; set; }
            public string State { get; set; } = string.Empty;
            public AuthorNode? Author { get; set; }
            public AuthorNode? MergedBy { get; set; }
            public LabelsData? Labels { get; set; }
        }
    }

    private class IssuesResponse
    {
        public OrganizationData? Organization { get; set; }

        public class OrganizationData
        {
            public RepositoryData? Repository { get; set; }
        }

        public class RepositoryData
        {
            public IssuesData? Issues { get; set; }
        }

        public class IssuesData
        {
            public List<IssueNode> Nodes { get; set; } = new();
            public PageInfo PageInfo { get; set; } = new();
        }

        public class IssueNode
        {
            public string Title { get; set; } = string.Empty;
            public int Number { get; set; }
            public string Url { get; set; } = string.Empty;
            public string CreatedAt { get; set; } = string.Empty;
            public string UpdatedAt { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public AuthorNode? Author { get; set; }
            public LabelsData? Labels { get; set; }
        }
    }

    private class AuthorNode
    {
        public string Login { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    private class LabelsData
    {
        public List<LabelNode>? Nodes { get; set; }
    }

    private class LabelNode
    {
        public string Name { get; set; } = string.Empty;
    }

    private class PageInfo
    {
        public bool HasNextPage { get; set; }
        public string? EndCursor { get; set; }
    }

    private class RateLimitResponse
    {
        public RateLimitData? RateLimit { get; set; }

        public class RateLimitData
        {
            public int Limit { get; set; }
            public int Remaining { get; set; }
            public int Used { get; set; }
            public string ResetAt { get; set; } = string.Empty;
        }
    }

    public class RateLimitInfo
    {
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public int Used { get; set; }
        public DateTime ResetAt { get; set; }
    }

    private class TeamMembersResponse
    {
        public OrganizationData? Organization { get; set; }

        public class OrganizationData
        {
            public TeamData? Team { get; set; }
        }

        public class TeamData
        {
            public MembersData? Members { get; set; }
        }

        public class MembersData
        {
            public List<MemberNode> Nodes { get; set; } = new();
            public PageInfo PageInfo { get; set; } = new();
        }

        public class MemberNode
        {
            public string Login { get; set; } = string.Empty;
            public string? Name { get; set; }
        }
    }

    private class DiscussionsResponse
    {
        public RepositoryData? Repository { get; set; }

        public class RepositoryData
        {
            public DiscussionsData? Discussions { get; set; }
        }

        public class DiscussionsData
        {
            public List<DiscussionNode> Nodes { get; set; } = new();
            public PageInfo PageInfo { get; set; } = new();
        }

        public class DiscussionNode
        {
            public string Title { get; set; } = string.Empty;
            public int Number { get; set; }
            public string Url { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public string CreatedAt { get; set; } = string.Empty;
            public string UpdatedAt { get; set; } = string.Empty;
            public CategoryNode? Category { get; set; }
            public LabelsData? Labels { get; set; }
        }

        public class CategoryNode
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}

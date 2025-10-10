using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

namespace UmbracoCommunity.Web.Features.GitHubSync.Jobs;

public class FetchRecentPullRequestsJob
{
    private readonly GitHubApiClient _apiClient;
    private readonly GitHubCosmosDbStore _dataStore;

    public FetchRecentPullRequestsJob(
        GitHubApiClient apiClient,
        GitHubCosmosDbStore dataStore)
    {
        _apiClient = apiClient;
        _dataStore = dataStore;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 600 })]
    public async Task ExecuteAsync(PerformContext? context, string? specificRepo = null, CancellationToken cancellationToken = default)
    {
        var repo = specificRepo ?? "all";
        context?.WriteLine($"Starting FetchRecentPullRequestsJob for repo: {repo}");
        Action<string>? log = context != null ? context.WriteLine : null;

        try
        {
            // Log rate limit before starting
            context?.WriteLine("Checking GitHub API rate limits...");
            await _apiClient.GetRateLimitInfoAsync(log, cancellationToken);

            context?.WriteLine("Fetching recent pull requests from GitHub...");
            var pullRequests = await _apiClient.FetchRecentPullRequestsAsync(specificRepo, log, cancellationToken);

            context?.WriteLine($"Fetched {pullRequests.Count} recent pull requests");

            context?.WriteLine("Saving to database...");
            var result = _dataStore.UpsertPullRequests(pullRequests);

            var message = $"Sync completed: {result.Added} added, {result.Updated} updated, {result.Total} total";
            context?.WriteLine(message);

            // Log rate limit after completion
            context?.WriteLine("Final rate limit status:");
            await _apiClient.GetRateLimitInfoAsync(log, cancellationToken);
        }
        catch (Exception ex)
        {
            context?.WriteLine($"ERROR: {ex.Message}");
            throw;
        }
    }
}

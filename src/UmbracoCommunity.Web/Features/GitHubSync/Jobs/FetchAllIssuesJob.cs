using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

namespace UmbracoCommunity.Web.Features.GitHubSync.Jobs;

public class FetchAllIssuesJob
{
    private readonly GitHubApiClient _apiClient;
    private readonly GitHubDataStore _dataStore;

    public FetchAllIssuesJob(
        GitHubApiClient apiClient,
        GitHubDataStore dataStore)
    {
        _apiClient = apiClient;
        _dataStore = dataStore;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 600 })]
    public async Task ExecuteAsync(PerformContext? context, string? specificRepo = null, CancellationToken cancellationToken = default)
    {
        var repo = specificRepo ?? "all";
        context?.WriteLine($"Starting FetchAllIssuesJob for repo: {repo}");
        Action<string>? log = context != null ? context.WriteLine : null;

        try
        {
            // Log rate limit before starting
            context?.WriteLine("Checking GitHub API rate limits...");
            await _apiClient.GetRateLimitInfoAsync(log, cancellationToken);

            context?.WriteLine("Fetching issues from GitHub...");
            var issues = await _apiClient.FetchAllIssuesAsync(specificRepo, log, cancellationToken);

            context?.WriteLine($"Fetched {issues.Count} issues");

            context?.WriteLine("Saving to database...");
            var result = _dataStore.UpsertIssues(issues);

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

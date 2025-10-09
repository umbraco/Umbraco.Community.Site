using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

namespace UmbracoCommunity.Web.Features.GitHubSync.Jobs;

public class FetchReleaseDiscussionsJob
{
    private readonly GitHubApiClient _apiClient;
    private readonly GitHubDataStore _dataStore;

    public FetchReleaseDiscussionsJob(
        GitHubApiClient apiClient,
        GitHubDataStore dataStore)
    {
        _apiClient = apiClient;
        _dataStore = dataStore;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 600 })]
    public async Task ExecuteAsync(PerformContext? context, string? specificRepo = null, CancellationToken cancellationToken = default)
    {
        var repo = specificRepo ?? "Umbraco-CMS";
        context?.WriteLine($"Starting FetchReleaseDiscussionsJob for repo: {repo}");
        Action<string>? log = context != null ? context.WriteLine : null;

        try
        {
            // Log rate limit before starting
            context?.WriteLine("Checking GitHub API rate limits...");
            await _apiClient.GetRateLimitInfoAsync(log, cancellationToken);

            context?.WriteLine($"Fetching release discussions from GitHub for '{repo}' in 'releases' category...");
            var discussions = await _apiClient.FetchDiscussionsByCategoryAsync(repo, "releases", log, cancellationToken);

            context?.WriteLine($"Fetched {discussions.Count} release discussions");

            context?.WriteLine("Saving to database...");
            var result = _dataStore.UpsertDiscussions(discussions);

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

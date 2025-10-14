using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Caching.Memory;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Web.Features.GitHubSync.Jobs;

public class FetchRecentPullRequestsJob
{
    private readonly GitHubApiClient _apiClient;
    private readonly GitHubSqlStore _dataStore;
    private readonly IMemoryCache _memoryCache;

    public FetchRecentPullRequestsJob(
        GitHubApiClient apiClient,
        GitHubSqlStore dataStore,
        IMemoryCache memoryCache)
    {
        _apiClient = apiClient;
        _dataStore = dataStore;
        _memoryCache = memoryCache;
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

            // Clear the all releases cache since data has changed
            context?.WriteLine("Clearing all releases cache...");
            _memoryCache.Remove(AllReleasesPageViewModelBuilder.CacheKey);

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

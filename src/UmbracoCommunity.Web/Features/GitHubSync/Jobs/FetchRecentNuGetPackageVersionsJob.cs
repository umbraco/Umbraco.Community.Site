using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Web.Features.GitHubSync.Jobs;

public class FetchRecentNuGetPackageVersionsJob
{
    private readonly NuGetApiClient _nugetClient;
    private readonly GitHubSqlStore _dataStore;
    private readonly GitHubSyncOptions _options;
    private readonly IMemoryCache _memoryCache;

    public FetchRecentNuGetPackageVersionsJob(
        NuGetApiClient nugetClient,
        GitHubSqlStore dataStore,
        IOptions<GitHubSyncOptions> options,
        IMemoryCache memoryCache)
    {
        _nugetClient = nugetClient;
        _dataStore = dataStore;
        _options = options.Value;
        _memoryCache = memoryCache;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 600 })]
    public async Task ExecuteAsync(PerformContext? context, CancellationToken cancellationToken = default)
    {
        context?.WriteLine("Starting FetchRecentNuGetPackageVersionsJob");

        try
        {
            // Get all repositories that have a NuGet package configured
            var repositoriesWithNuGet = _options.Repositories
                .Where(r => r.HasNuGetPackage)
                .ToList();

            if (!repositoriesWithNuGet.Any())
            {
                context?.WriteLine("No repositories with NuGet package IDs configured");
                return;
            }

            var totalAdded = 0;
            var totalUpdated = 0;

            foreach (var repo in repositoriesWithNuGet)
            {
                var packageIds = repo.GetNuGetPackageIds();
                foreach (var packageId in packageIds)
                {
                    context?.WriteLine($"Fetching latest 20 NuGet versions for {repo.Name} (package: {packageId})...");

                    var versions = await _nugetClient.GetPackageVersionsAsync(packageId, maxCount: 20);

                    context?.WriteLine($"Found {versions.Count} versions");

                    var result = _dataStore.UpsertNuGetPackageVersions(packageId, versions);

                    totalAdded += result.Added;
                    totalUpdated += result.Updated;

                    context?.WriteLine($"  {result.Added} added, {result.Updated} updated");
                }
            }

            var message = $"Sync completed: {totalAdded} added, {totalUpdated} updated across all packages";
            context?.WriteLine(message);

            // Clear the all releases cache since data has changed
            context?.WriteLine("Clearing all releases cache...");
            _memoryCache.Remove(AllReleasesPageViewModelBuilder.CacheKey);
        }
        catch (Exception ex)
        {
            context?.WriteLine($"ERROR: {ex.Message}");
            throw;
        }
    }
}

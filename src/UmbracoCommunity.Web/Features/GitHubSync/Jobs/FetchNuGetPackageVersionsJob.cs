using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

namespace UmbracoCommunity.Web.Features.GitHubSync.Jobs;

public class FetchNuGetPackageVersionsJob
{
    private readonly NuGetApiClient _nugetClient;
    private readonly GitHubSqlStore _dataStore;
    private readonly GitHubSyncOptions _options;

    public FetchNuGetPackageVersionsJob(
        NuGetApiClient nugetClient,
        GitHubSqlStore dataStore,
        IOptions<GitHubSyncOptions> options)
    {
        _nugetClient = nugetClient;
        _dataStore = dataStore;
        _options = options.Value;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 600 })]
    public async Task ExecuteAsync(PerformContext? context, CancellationToken cancellationToken = default)
    {
        context?.WriteLine("Starting FetchNuGetPackageVersionsJob");

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
                context?.WriteLine($"Fetching NuGet versions for {repo.Name} (package: {repo.NuGetPackageId})...");

                var versions = await _nugetClient.GetPackageVersionsAsync(repo.NuGetPackageId!);

                context?.WriteLine($"Found {versions.Count} versions");

                var result = _dataStore.UpsertNuGetPackageVersions(repo.NuGetPackageId!, versions);

                totalAdded += result.Added;
                totalUpdated += result.Updated;

                context?.WriteLine($"  {result.Added} added, {result.Updated} updated");
            }

            var message = $"Sync completed: {totalAdded} added, {totalUpdated} updated across all packages";
            context?.WriteLine(message);
        }
        catch (Exception ex)
        {
            context?.WriteLine($"ERROR: {ex.Message}");
            throw;
        }
    }
}

using System.Text.Json;
using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Web.Features.GitHubSync.Jobs;

public class FetchHqMembersJob
{
    private readonly GitHubApiClient _apiClient;
    private readonly GitHubSqlStore _dataStore;
    private readonly GitHubSyncOptions _options;

    public FetchHqMembersJob(
        GitHubApiClient apiClient,
        GitHubSqlStore dataStore,
        IOptions<GitHubSyncOptions> options)
    {
        _apiClient = apiClient;
        _dataStore = dataStore;
        _options = options.Value;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 600 })]
    public async Task ExecuteAsync(PerformContext? context, CancellationToken cancellationToken = default)
    {
        context?.WriteLine("Starting FetchHqMembersJob");
        Action<string>? log = context != null ? context.WriteLine : null;

        try
        {
            // Log rate limit before starting
            context?.WriteLine("Checking GitHub API rate limits...");
            await _apiClient.GetRateLimitInfoAsync(log, cancellationToken);

            // Load existing members from database to preserve their periods
            var existingMembersFromDb = _dataStore.GetHqMembers().ToDictionary(
                m => m.Login,
                StringComparer.OrdinalIgnoreCase);

            context?.WriteLine($"Found {existingMembersFromDb.Count} existing HQ members in database");

            // Fetch current team members from GitHub
            if (_options.HqOnlyTeams.Count > 0)
            {
                context?.WriteLine($"Fetching members from {_options.HqOnlyTeams.Count} HQ teams...");
                var currentMembers = await _apiClient.FetchOrganizationTeamMembersAsync(
                    _options.HqOnlyTeams, log, cancellationToken);

                context?.WriteLine($"Found {currentMembers.Count} current team members");

                // Update existing members or add new ones
                foreach (var (login, name) in currentMembers)
                {
                    if (existingMembersFromDb.TryGetValue(login, out var existingMember))
                    {
                        // Update name but preserve periods
                        existingMember.Name = name;
                    }
                    else
                    {
                        // Add new member with open-ended period
                        existingMembersFromDb[login] = new GitHubHqMember
                        {
                            Id = login,
                            Login = login,
                            Name = name,
                            Periods = new List<EmploymentPeriod>
                            {
                                new EmploymentPeriod { Start = null, End = null }
                            }
                        };
                    }
                }
            }
            else
            {
                context?.WriteLine("Warning: No HQ teams configured in GitHubSync:HqOnlyTeams");
            }

            // Save to database
            context?.WriteLine("Saving to database...");
            var result = _dataStore.UpsertHqMembers(existingMembersFromDb.Values);

            var message = $"Sync completed: {result.Added} added, {result.Updated} updated, {result.Total} total HQ members";
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

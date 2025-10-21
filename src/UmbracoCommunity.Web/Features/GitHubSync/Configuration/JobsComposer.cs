using Hangfire;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Composing;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Jobs;

namespace UmbracoCommunity.Web.Features.GitHubSync.Configuration;

[ComposeAfter(typeof(Cultiv.Hangfire.HangfireComposer))]
public class JobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Get configured repositories
        var repositories = builder.Config.GetSection("GitHubSync:Repositories")
            .Get<List<RepositoryConfig>>() ?? new List<RepositoryConfig>();

        // Full sync jobs - Weekly on Sunday at 2 AM
        // Create separate jobs for each configured repository
        foreach (var repo in repositories)
        {
            var repoName = repo.Name;
            var sanitizedRepoName = repoName.ToLowerInvariant().Replace(".", "-");

            RecurringJob.AddOrUpdate<FetchAllPullRequestsJob>(
                $"fetch-all-prs-{sanitizedRepoName}",
                job => job.ExecuteAsync(null, repoName, CancellationToken.None),
                GetCronExpression("0 2 * * 0"), // Cron: Sunday at 2 AM
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });

            RecurringJob.AddOrUpdate<FetchAllIssuesJob>(
                $"fetch-all-issues-{sanitizedRepoName}",
                job => job.ExecuteAsync(null, repoName, CancellationToken.None),
                GetCronExpression("0 2 * * 0"), // Cron: Sunday at 2 AM
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });
        }

        // Recent sync jobs - Every 6 hours
        RecurringJob.AddOrUpdate<FetchRecentPullRequestsJob>(
            "fetch-recent-prs",
            job => job.ExecuteAsync(null, null, CancellationToken.None),
            GetCronExpression("0 */1 * * *"), // Cron: Every 1 hours
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        RecurringJob.AddOrUpdate<FetchRecentIssuesJob>(
            "fetch-recent-issues",
            job => job.ExecuteAsync(null, null, CancellationToken.None),
            GetCronExpression("0 */1 * * *"), // Cron: Every 1 hours
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // HQ members sync - Weekly on Sunday at 3 AM
        RecurringJob.AddOrUpdate<FetchHqMembersJob>(
            "fetch-hq-members",
            job => job.ExecuteAsync(null, CancellationToken.None),
            GetCronExpression("0 3 * * 0"), // Cron: Sunday at 3 AM
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Release discussions sync - Daily at 4 AM
        RecurringJob.AddOrUpdate<FetchReleaseDiscussionsJob>(
            "fetch-release-discussions",
            job => job.ExecuteAsync(null, null, CancellationToken.None),
            GetCronExpression("0 */1 * * *"), // Cron: Daily at 4 AM
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // NuGet package versions sync - Daily at 5 AM (fetch ALL versions)
        RecurringJob.AddOrUpdate<FetchNuGetPackageVersionsJob>(
            "fetch-nuget-package-versions",
            job => job.ExecuteAsync(null, CancellationToken.None),
            GetCronExpression("0 5 * * *"), // Cron: Daily at 5 AM
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Recent NuGet package versions sync - Every 15 minutes (fetch latest 20)
        RecurringJob.AddOrUpdate<FetchRecentNuGetPackageVersionsJob>(
            "fetch-recent-nuget-package-versions",
            job => job.ExecuteAsync(null, CancellationToken.None),
            GetCronExpression("*/15 * * * *"), // Cron: Every 15 minutes
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    private static string GetCronExpression(string productionCron)
    {
        var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
        return isProduction ? productionCron : Cron.Never();
    }
}
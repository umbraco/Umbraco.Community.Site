using Hangfire;
using Umbraco.Cms.Core.Composing;
using UmbracoCommunity.Web.Features.GitHubSync.Jobs;

namespace UmbracoCommunity.Web.Features.GitHubSync.Configuration;

[ComposeAfter(typeof(Cultiv.Hangfire.HangfireComposer))]
public class JobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Full sync jobs - Weekly on Sunday at 2 AM
        RecurringJob.AddOrUpdate<FetchAllPullRequestsJob>(
            "fetch-all-prs",
            job => job.ExecuteAsync(null, null, CancellationToken.None),
            GetCronExpression("0 2 * * 0"), // Cron: Sunday at 2 AM
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        RecurringJob.AddOrUpdate<FetchAllIssuesJob>(
            "fetch-all-issues",
            job => job.ExecuteAsync(null, null, CancellationToken.None),
            GetCronExpression("0 2 * * 0"), // Cron: Sunday at 2 AM
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Recent sync jobs - Every 6 hours
        RecurringJob.AddOrUpdate<FetchRecentPullRequestsJob>(
            "fetch-recent-prs",
            job => job.ExecuteAsync(null, null, CancellationToken.None),
            GetCronExpression("0 */6 * * *"), // Cron: Every 6 hours
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        RecurringJob.AddOrUpdate<FetchRecentIssuesJob>(
            "fetch-recent-issues",
            job => job.ExecuteAsync(null, null, CancellationToken.None),
            GetCronExpression("0 */6 * * *"), // Cron: Every 6 hours
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
            GetCronExpression("0 4 * * *"), // Cron: Daily at 4 AM
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // NuGet package versions sync - Daily at 5 AM
        RecurringJob.AddOrUpdate<FetchNuGetPackageVersionsJob>(
            "fetch-nuget-package-versions",
            job => job.ExecuteAsync(null, CancellationToken.None),
            GetCronExpression("0 5 * * *"), // Cron: Daily at 5 AM
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
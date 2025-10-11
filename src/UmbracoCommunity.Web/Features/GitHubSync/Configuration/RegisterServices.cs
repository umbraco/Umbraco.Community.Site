using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Jobs;

namespace UmbracoCommunity.Web.Features.GitHubSync.Configuration;

public class RegisterServices : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Register HttpClient
        builder.Services.AddHttpClient();
        builder.Services.Configure<GitHubSyncOptions>(
            builder.Config.GetSection(GitHubSyncOptions.SectionName));

        // Register SQL Server DbContext (using Umbraco's connection string)
        builder.Services.AddDbContextFactory<GitHubDbContext>((serviceProvider, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("UmbracoCommunity.Web");
            });
        });

        // Register automatic database migration on startup
        builder.Services.AddHostedService<DatabaseMigrationHostedService>();

        // Register infrastructure
        builder.Services.AddSingleton<GitHubSqlStore>();
        builder.Services.AddScoped<GitHubApiClient>();

        // Register jobs
        builder.Services.AddScoped<FetchAllPullRequestsJob>();
        builder.Services.AddScoped<FetchAllIssuesJob>();
        builder.Services.AddScoped<FetchRecentPullRequestsJob>();
        builder.Services.AddScoped<FetchRecentIssuesJob>();
        builder.Services.AddScoped<FetchHqMembersJob>();
        builder.Services.AddScoped<FetchReleaseDiscussionsJob>();

    }
}
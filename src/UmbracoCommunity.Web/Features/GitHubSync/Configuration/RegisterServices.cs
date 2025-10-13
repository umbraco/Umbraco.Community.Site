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

        // Register DbContext (using Umbraco's connection string and provider)
        builder.Services.AddDbContextFactory<GitHubDbContext>((serviceProvider, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                // Resolve |DataDirectory| to the correct Umbraco data directory
                var hostingEnvironment = serviceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                var dataDirectory = System.IO.Path.Combine(hostingEnvironment.ContentRootPath, "umbraco", "Data");
                var resolvedConnectionString = connectionString?.Replace("|DataDirectory|", dataDirectory);

                options.UseSqlite(resolvedConnectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly("UmbracoCommunity.Web");
                });
            }
            else
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("UmbracoCommunity.Web");
                });
            }

            // Suppress the pending model changes warning since we're handling both SQL Server and SQLite
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register automatic database migration on startup
        builder.Services.AddHostedService<DatabaseMigrationHostedService>();

        // Register infrastructure
        builder.Services.AddSingleton<GitHubSqlStore>();
        builder.Services.AddScoped<GitHubApiClient>();
        builder.Services.AddHttpClient<NuGetApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });

        // Register jobs
        builder.Services.AddScoped<FetchAllPullRequestsJob>();
        builder.Services.AddScoped<FetchAllIssuesJob>();
        builder.Services.AddScoped<FetchRecentPullRequestsJob>();
        builder.Services.AddScoped<FetchRecentIssuesJob>();
        builder.Services.AddScoped<FetchHqMembersJob>();
        builder.Services.AddScoped<FetchReleaseDiscussionsJob>();
        builder.Services.AddScoped<FetchNuGetPackageVersionsJob>();
        builder.Services.AddScoped<FetchRecentNuGetPackageVersionsJob>();

    }
}
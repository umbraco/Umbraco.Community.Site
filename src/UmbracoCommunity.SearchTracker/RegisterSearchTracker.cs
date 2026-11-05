using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using UmbracoCommunity.SearchTracker.Api;
using UmbracoCommunity.SearchTracker.Infrastructure;
using UmbracoCommunity.SearchTracker.Recording;
using UmbracoCommunity.SearchTracker.Reporting;

namespace UmbracoCommunity.SearchTracker;

public sealed class RegisterSearchTracker : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<SearchTrackerOptions>(builder.Config.GetSection(SearchTrackerOptions.SectionName));
        builder.Services.PostConfigure<SearchTrackerOptions>(o =>
            o.MaxTermLength = Math.Clamp(o.MaxTermLength, 1, SearchTrackerDbContext.TermMaxLength));

        builder.Services.AddDbContextFactory<SearchTrackerDbContext>((sp, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var dataDir = Path.Combine(env.ContentRootPath, "umbraco", "Data");
                var resolved = connectionString?.Replace("|DataDirectory|", dataDir);
                options.UseSqlite(resolved, sqlite =>
                    sqlite.MigrationsAssembly("UmbracoCommunity.SearchTracker"));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("UmbracoCommunity.SearchTracker");
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<SearchTrackerSchemaState>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SearchTrackerMigrationNotificationHandler>();

        builder.Services.AddSingleton<SearchEventChannel>();
        builder.Services.AddSingleton<ISearchTermRecorder, SearchTermRecorder>();
        builder.Services.AddHostedService<SearchEventWriterService>();
        builder.Services.AddHostedService<SearchTrackerRetentionService>();

        builder.Services.AddScoped<ISiteScopeService, SiteScopeService>();
        builder.Services.AddSingleton<ISearchReportService, SearchReportService>();

        builder.AddBackOfficeOpenApiDocument(SearchTrackerApiConstants.ApiName, doc => doc
            .WithTitle("Umbraco Community Search Tracker Backoffice API")
            .WithBackOfficeAuthentication());
    }
}

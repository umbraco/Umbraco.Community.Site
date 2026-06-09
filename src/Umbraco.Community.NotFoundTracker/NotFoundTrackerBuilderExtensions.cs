using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Controllers;
using Umbraco.Extensions;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Recording;
using Umbraco.Community.NotFoundTracker.Routing;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker;

public static class NotFoundTrackerBuilderExtensions
{
    public static IUmbracoBuilder AddNotFoundTracker(this IUmbracoBuilder builder)
    {
        builder.Services.Configure<NotFoundTrackerOptions>(
            builder.Config.GetSection(Constants.ConfigurationSectionName));

        builder.Services.AddDbContextFactory<NotFoundTrackerDbContext>((sp, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var dataDir = Path.Combine(env.ContentRootPath, "umbraco", "Data");
                var resolved = connectionString?.Replace("|DataDirectory|", dataDir);
                options.UseSqlite(resolved, sqlite =>
                    sqlite.MigrationsAssembly("Umbraco.Community.NotFoundTracker"));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("Umbraco.Community.NotFoundTracker");

                    // This context shares the Umbraco Azure SQL connection and writes on every
                    // front-end 404, so it sits in the blast radius of traffic spikes and the
                    // database's connection/DTU limits. Retry transient faults (connection
                    // limit reached, timeouts, deadlocks) instead of dropping 404 records.
                    // Safe to enable: no code path uses manual transactions, which a retrying
                    // execution strategy would otherwise reject.
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddSingleton<HostnameNormalizationService>();
        builder.Services.AddHostedService<NotFoundTrackerMigrationHostedService>();

        // Recording pipeline.
        builder.Services.AddSingleton<NotFoundHitChannel>();
        builder.Services.AddHostedService<NotFoundHitWriterService>();
        builder.Services.AddHostedService<NotFoundRetentionService>();

        // Ignore matcher — real implementation wired in Plan 2.
        builder.Services.AddSingleton<IgnoreRuleLoader>();
        builder.Services.AddSingleton<INotFoundIgnoreRuleMatcher, IgnoreRuleMatcher>();
        builder.Services.AddSingleton<AutoPresetSeedingService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AutoPresetSeedingService>());

        // Management services.
        builder.Services.AddScoped<INotFoundHitService, NotFoundHitService>();
        builder.Services.AddScoped<INotFoundIgnoreRuleService, NotFoundIgnoreRuleService>();
        builder.Services.AddScoped<INotFoundUserScopeService, NotFoundUserScopeService>();
        builder.Services.AddScoped<NotFoundHostnameGroupService>();
        builder.Services.AddScoped<INotFoundRedirectService, NotFoundRedirectService>();

        // Fail-fast guard: if the host forgot to register an INotFoundPageResolver,
        // surface an actionable error at the first 404 instead of a generic DI failure.
        // TryAddSingleton lets the host's own registration win when present.
        builder.Services.TryAddSingleton<INotFoundPageResolver>(_ =>
            throw new InvalidOperationException(
                "Umbraco.Community.NotFoundTracker: no INotFoundPageResolver registered. " +
                "The host must call builder.Services.AddSingleton<INotFoundPageResolver, YourResolver>() " +
                "after builder.AddNotFoundTracker()."));

        // Content finder. The host must register an INotFoundPageResolver.
        builder.SetContentLastChanceFinder<NotFoundTrackingContentFinder>();

        // Swagger document for the management API.
        builder.Services.Configure<SwaggerGenOptions>(opt =>
        {
            opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
            {
                Title = "Umbraco Community NotFoundTracker Backoffice API",
                Version = "1.0",
            });
            opt.OperationFilter<NotFoundTrackerOperationSecurityFilter>();
        });

        return builder;
    }
}

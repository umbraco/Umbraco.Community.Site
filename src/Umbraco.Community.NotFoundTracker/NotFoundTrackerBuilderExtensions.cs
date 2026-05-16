using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;

namespace Umbraco.Community.NotFoundTracker;

public static class NotFoundTrackerBuilderExtensions
{
    /// <summary>
    /// Registers all services for the NotFoundTracker package. Call from a host composer.
    /// The host MUST also register an <c>INotFoundPageResolver</c> implementation —
    /// it tells the package which content node to render for an unrecorded 404.
    /// </summary>
    public static IUmbracoBuilder AddNotFoundTracker(this IUmbracoBuilder builder)
    {
        // Bind options from configuration (section "NotFoundTracker").
        builder.Services.Configure<NotFoundTrackerOptions>(
            builder.Config.GetSection(Constants.ConfigurationSectionName));

        // EF Core DbContext factory — same pattern as BlockRestrictions.
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
                    sql.MigrationsAssembly("Umbraco.Community.NotFoundTracker"));
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddHostedService<NotFoundTrackerMigrationHostedService>();

        // Recording pipeline, finder, retention — all added in later tasks of this plan.
        return builder;
    }
}

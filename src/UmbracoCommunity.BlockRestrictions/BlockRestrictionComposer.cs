using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using UmbracoCommunity.BlockRestrictions.Infrastructure;

namespace UmbracoCommunity.BlockRestrictions;

/// <summary>
/// Umbraco Composer that registers all Block Restrictions services on startup.
/// Composers are Umbraco's dependency injection extension point — they run during
/// app startup and are discovered automatically via assembly scanning.
///
/// This composer sets up:
///   1. EF Core DbContext factory (supports both SQLite and SQL Server)
///   2. Swagger/OpenAPI documentation with backoffice auth
///   3. Database migration hosted service
///   4. Scoped services (store + business logic)
/// </summary>
public class BlockRestrictionComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Register the EF Core DbContext as a factory. Using a factory (rather than a
        // direct DbContext registration) lets us create short-lived contexts per operation,
        // which avoids issues with scoped lifetime mismatches in hosted services.
        builder.Services.AddDbContextFactory<BlockRestrictionDbContext>((serviceProvider, options) =>
        {
            // Read the Umbraco database connection string and provider from configuration.
            // We share the same database as Umbraco but manage our own table via EF Core.
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                // SQLite: resolve the |DataDirectory| placeholder that Umbraco uses
                // in its connection string to point to the umbraco/Data/ folder.
                var hostingEnvironment = serviceProvider
                    .GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                var dataDirectory = Path.Combine(hostingEnvironment.ContentRootPath, "umbraco", "Data");
                var resolvedConnectionString = connectionString?.Replace("|DataDirectory|", dataDirectory);

                options.UseSqlite(resolvedConnectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly("UmbracoCommunity.BlockRestrictions");
                });
            }
            else
            {
                // SQL Server: use the connection string as-is.
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("UmbracoCommunity.BlockRestrictions");
                });
            }

            // Suppress the EF Core warning about pending model changes.
            // Our migrations are applied by the hosted service on startup.
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register a Swagger/OpenAPI document for this package's endpoints.
        // The security filter enables the Umbraco backoffice "Authorize" button in Swagger UI,
        // so you can test the API endpoints while authenticated.
        builder.Services.Configure<SwaggerGenOptions>(opt =>
        {
            opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
            {
                Title = "Umbraco Community Block Restrictions Backoffice API",
                Version = "1.0",
            });

            opt.OperationFilter<BlockRestrictionsOperationSecurityFilter>();
        });

        // Apply database migrations after Umbraco has finished booting. Using a notification
        // handler (rather than an IHostedService) avoids racing Umbraco's unattended installer
        // for the SQLite write lock on a fresh install — see issue #132.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, BlockRestrictionMigrationNotificationHandler>();

        // File-based persistence: singleton service for reading/writing JSON rule files.
        // Import from files is triggered manually via the backoffice dashboard.
        builder.Services.AddSingleton<BlockRestrictionFileService>();

        // Register the data access layer and business logic service as scoped
        // (one instance per HTTP request, matching Umbraco's service lifetime).
        builder.Services.AddScoped<BlockRestrictionStore>();
        builder.Services.AddScoped<BlockRestrictionService>();
    }

    /// <summary>
    /// Security filter that associates our API endpoints with the Umbraco backoffice
    /// authentication scheme. Extends Umbraco's base class to inherit the standard
    /// backoffice OAuth2 security requirements for Swagger documentation.
    /// </summary>
    private class BlockRestrictionsOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.ApiName;
    }
}

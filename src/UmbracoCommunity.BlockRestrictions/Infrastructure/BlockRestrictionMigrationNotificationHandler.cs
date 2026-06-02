using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations once Umbraco has finished booting.
///
/// Why a notification handler instead of <see cref="Microsoft.Extensions.Hosting.IHostedService"/>:
/// on a fresh install Umbraco runs its unattended installer during host startup, which creates
/// and populates the SQLite database. A hosted service runs concurrently with that installer and
/// can block on a SQLite write lock for several minutes (issue #132). Deferring to
/// <see cref="UmbracoApplicationStartedNotification"/> guarantees Umbraco has finished its own
/// database setup before we touch the file.
/// </summary>
public class BlockRestrictionMigrationNotificationHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IDbContextFactory<BlockRestrictionDbContext> _contextFactory;
    private readonly ILogger<BlockRestrictionMigrationNotificationHandler> _logger;

    public BlockRestrictionMigrationNotificationHandler(
        IDbContextFactory<BlockRestrictionDbContext> contextFactory,
        ILogger<BlockRestrictionMigrationNotificationHandler> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying BlockRestrictions database migrations...");
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var connectionString = context.Database.GetConnectionString();
            var isSqlite = context.Database.IsSqlite();

            // SQLite: ensure the directory for the database file exists.
            // Umbraco will have created it during install, but belt-and-braces for non-install boots.
            if (isSqlite && connectionString != null)
            {
                var builder = new SqliteConnectionStringBuilder(connectionString);
                var dbPath = builder.DataSource;
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation("Created directory for SQLite database: {Directory}", directory);
                }
            }

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending BlockRestrictions migration(s): {Migrations}",
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("BlockRestrictions migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("BlockRestrictions database is up to date");
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw — a migration failure shouldn't prevent the app from starting.
            // The restriction features will degrade gracefully (fail-open).
            _logger.LogError(ex, "Failed to apply BlockRestrictions database migrations");
        }
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// Hosted service that automatically applies pending EF Core migrations on application startup.
/// This ensures the BlockRestrictionRules table is created/updated without requiring manual
/// migration commands. Runs once on startup and then completes.
///
/// For SQLite, also ensures the database directory exists (Umbraco may not have created
/// the Data directory yet on a fresh install).
/// </summary>
public class BlockRestrictionMigrationHostedService : IHostedService
{
    private readonly IDbContextFactory<BlockRestrictionDbContext> _contextFactory;
    private readonly ILogger<BlockRestrictionMigrationHostedService> _logger;

    public BlockRestrictionMigrationHostedService(
        IDbContextFactory<BlockRestrictionDbContext> contextFactory,
        ILogger<BlockRestrictionMigrationHostedService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying BlockRestrictions database migrations...");
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var connectionString = context.Database.GetConnectionString();
            var isSqlite = context.Database.IsSqlite();

            // SQLite: ensure the directory for the database file exists.
            // On a fresh Umbraco install the umbraco/Data/ folder may not exist yet.
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

            // Check for and apply any pending migrations.
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

    /// <summary>No cleanup needed on shutdown.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

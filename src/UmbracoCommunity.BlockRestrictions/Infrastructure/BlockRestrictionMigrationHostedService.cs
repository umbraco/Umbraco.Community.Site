using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

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
            _logger.LogError(ex, "Failed to apply BlockRestrictions database migrations");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

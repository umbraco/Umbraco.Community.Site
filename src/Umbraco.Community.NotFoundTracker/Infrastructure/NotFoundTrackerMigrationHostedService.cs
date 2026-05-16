using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations for the NotFoundTracker schema on application startup.
/// Mirrors the pattern in <c>BlockRestrictionMigrationHostedService</c> — ensures the SQLite
/// data directory exists before connecting, then applies any pending migrations.
/// </summary>
public class NotFoundTrackerMigrationHostedService : IHostedService
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly ILogger<NotFoundTrackerMigrationHostedService> _logger;

    public NotFoundTrackerMigrationHostedService(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        ILogger<NotFoundTrackerMigrationHostedService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying NotFoundTracker database migrations...");
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var connectionString = context.Database.GetConnectionString();
            if (context.Database.IsSqlite() && connectionString != null)
            {
                var builder = new SqliteConnectionStringBuilder(connectionString);
                var directory = Path.GetDirectoryName(builder.DataSource);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation("Created directory for SQLite database: {Directory}", directory);
                }
            }

            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                _logger.LogInformation("Applying {Count} pending NotFoundTracker migration(s): {Migrations}",
                    pending.Count(), string.Join(", ", pending));
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("NotFoundTracker migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("No pending NotFoundTracker migrations");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply NotFoundTracker migrations");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.BlogAnnouncements.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations for the blog-announcements schema on startup. Mirrors the
/// NotFoundTracker pattern: ensures the SQLite data directory exists before connecting, then
/// applies any pending migrations.
/// </summary>
public class BlogAnnouncementsMigrationHostedService : IHostedService
{
    private readonly IDbContextFactory<BlogAnnouncementsDbContext> _contextFactory;
    private readonly ILogger<BlogAnnouncementsMigrationHostedService> _logger;

    public BlogAnnouncementsMigrationHostedService(
        IDbContextFactory<BlogAnnouncementsDbContext> contextFactory,
        ILogger<BlogAnnouncementsMigrationHostedService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying BlogAnnouncements database migrations...");
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
                _logger.LogInformation("Applying {Count} pending BlogAnnouncements migration(s): {Migrations}",
                    pending.Count(), string.Join(", ", pending));
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("BlogAnnouncements migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("No pending BlogAnnouncements migrations");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply BlogAnnouncements migrations");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

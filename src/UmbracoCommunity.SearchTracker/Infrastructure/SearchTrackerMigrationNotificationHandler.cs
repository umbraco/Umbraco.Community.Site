using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoCommunity.SearchTracker.Infrastructure;

/// <summary>
/// Applies pending migrations once Umbraco has booted, avoiding the fresh-install SQLite lock race
/// (same approach as the BlogAnnouncements and NotFoundTracker handlers).
/// </summary>
public class SearchTrackerMigrationNotificationHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IDbContextFactory<SearchTrackerDbContext> _contextFactory;
    private readonly SearchTrackerSchemaState _schemaState;
    private readonly ILogger<SearchTrackerMigrationNotificationHandler> _logger;

    public SearchTrackerMigrationNotificationHandler(
        IDbContextFactory<SearchTrackerDbContext> contextFactory,
        SearchTrackerSchemaState schemaState,
        ILogger<SearchTrackerMigrationNotificationHandler> logger)
    {
        _contextFactory = contextFactory;
        _schemaState = schemaState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var connectionString = context.Database.GetConnectionString();
            if (context.Database.IsSqlite() && connectionString != null)
            {
                var directory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(connectionString).DataSource);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                _logger.LogInformation("Applying {Count} pending SearchTracker migration(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));
                await context.Database.MigrateAsync(cancellationToken);
            }

            _schemaState.MarkReady();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply SearchTracker migrations");
        }
    }
}

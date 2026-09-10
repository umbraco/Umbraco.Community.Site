using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoCommunity.MeetBooking.Storage;

/// <summary>
/// Applies pending EF Core migrations for the MeetBooking schema once Umbraco has finished booting.
/// </summary>
/// <remarks>
/// A notification handler rather than an <c>IHostedService</c>, for the same reason as
/// <c>NotFoundTrackerMigrationNotificationHandler</c>: on a fresh install Umbraco runs its unattended installer during
/// host startup, and a hosted service racing that installer can block on a SQLite write lock and fail with
/// "database table is locked".
/// </remarks>
public class MeetBookingMigrationNotificationHandler(
    IDbContextFactory<MeetBookingDbContext> contextFactory,
    ILogger<MeetBookingMigrationNotificationHandler> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

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
            if (pending.Count == 0)
            {
                logger.LogInformation("No pending MeetBooking migrations");
                return;
            }

            logger.LogInformation("Applying {Count} pending MeetBooking migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("MeetBooking migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply MeetBooking migrations");
        }
    }
}

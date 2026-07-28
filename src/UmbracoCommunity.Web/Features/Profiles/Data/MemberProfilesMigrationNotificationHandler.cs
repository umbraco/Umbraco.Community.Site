using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

/// <summary>
/// Applies pending EF Core migrations for the MemberProfiles schema once Umbraco has finished
/// booting.
///
/// Why a notification handler instead of <see cref="Microsoft.Extensions.Hosting.IHostedService"/>:
/// on a fresh install Umbraco runs its unattended installer during host startup, which creates
/// and populates the SQLite database. A hosted service runs concurrently with that installer and
/// can block on a SQLite write lock for well over 30s, throwing "database table is locked".
/// Deferring to <see cref="UmbracoApplicationStartedNotification"/> guarantees Umbraco has
/// finished its own database setup before we touch the file (mirrors
/// <c>BlockRestrictionMigrationNotificationHandler</c>, which fixed the same issue there).
/// </summary>
public class MemberProfilesMigrationNotificationHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IDbContextFactory<MemberProfilesDbContext> _contextFactory;
    private readonly ILogger<MemberProfilesMigrationNotificationHandler> _logger;

    public MemberProfilesMigrationNotificationHandler(
        IDbContextFactory<MemberProfilesDbContext> contextFactory,
        ILogger<MemberProfilesMigrationNotificationHandler> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying MemberProfiles database migrations...");
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
                _logger.LogInformation("Applying {Count} pending MemberProfiles migration(s): {Migrations}",
                    pending.Count(), string.Join(", ", pending));
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("MemberProfiles migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("No pending MemberProfiles migrations");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply MemberProfiles migrations");
        }
    }
}

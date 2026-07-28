using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations for the NotFoundTracker schema once Umbraco has finished
/// booting, then runs auto-preset seeding and hostname-normalization backfill.
///
/// Why a notification handler instead of <see cref="Microsoft.Extensions.Hosting.IHostedService"/>:
/// on a fresh install Umbraco runs its unattended installer during host startup, which creates
/// and populates the SQLite database. A hosted service runs concurrently with that installer and
/// can block on a SQLite write lock for well over 30s, throwing "database table is locked".
/// Deferring to <see cref="UmbracoApplicationStartedNotification"/> guarantees Umbraco has
/// finished its own database setup before we touch the file (mirrors
/// <c>BlockRestrictionMigrationNotificationHandler</c>, which fixed the same issue there).
/// </summary>
public class NotFoundTrackerMigrationNotificationHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IDbContextFactory<NotFoundTrackerDbContext> _contextFactory;
    private readonly HostnameNormalizationService _normalizer;
    private readonly AutoPresetSeedingService _autoPresetSeeding;
    private readonly ILogger<NotFoundTrackerMigrationNotificationHandler> _logger;

    public NotFoundTrackerMigrationNotificationHandler(
        IDbContextFactory<NotFoundTrackerDbContext> contextFactory,
        HostnameNormalizationService normalizer,
        AutoPresetSeedingService autoPresetSeeding,
        ILogger<NotFoundTrackerMigrationNotificationHandler> logger)
    {
        _contextFactory = contextFactory;
        _normalizer = normalizer;
        _autoPresetSeeding = autoPresetSeeding;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
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

            // Backfill normalization for rows recorded before UrlNormalizer stripped schemes
            // and trailing slashes. Idempotent: skips rows already in canonical form.
            await _normalizer.NormalizeAsync(cancellationToken);

            // Auto-preset seeding depends on the tables created above, so it runs here rather
            // than as its own hosted service (which could otherwise start before migrations do).
            await _autoPresetSeeding.SeedAndReconcileAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply NotFoundTracker migrations");
        }
    }
}

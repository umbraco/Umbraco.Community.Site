using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class DatabaseMigrationHostedService : IHostedService
{
    private readonly IDbContextFactory<GitHubDbContext> _contextFactory;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(
        IDbContextFactory<GitHubDbContext> contextFactory,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying GitHub database migrations...");
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Remove the old migration record that used non-prefixed table names
            var oldMigrationId = "20251011072916_InitialGitHubDatabase";
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM __EFMigrationsHistory WHERE MigrationId = {0}",
                oldMigrationId);
            _logger.LogInformation("Removed old migration record: {MigrationId}", oldMigrationId);

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

            _logger.LogInformation("Applied migrations: {AppliedCount} - {AppliedMigrations}", appliedMigrations.Count(), string.Join(", ", appliedMigrations));
            _logger.LogInformation("Pending migrations: {PendingCount}", pendingMigrations.Count());

            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying pending migrations: {Migrations}", string.Join(", ", pendingMigrations));
            }

            await context.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("GitHub database migrations completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply GitHub database migrations");
            // Don't throw - let the app start, but log the error
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

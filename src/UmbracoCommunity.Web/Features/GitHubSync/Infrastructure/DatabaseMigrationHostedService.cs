using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class DatabaseMigrationHostedService : IHostedService
{
    private readonly IDbContextFactory<GitHubDbContext> _contextFactory;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseMigrationHostedService(
        IDbContextFactory<GitHubDbContext> contextFactory,
        ILogger<DatabaseMigrationHostedService> logger,
        IConfiguration configuration)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying GitHub database migrations...");
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var isSqlite = context.Database.IsSqlite();

            // Check if tables already exist
            var tablesExist = false;
            try
            {
                if (isSqlite)
                {
                    // Check if GitHubDiscussions table exists in SQLite
                    using var command = context.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='GitHubDiscussions'";
                    await context.Database.OpenConnectionAsync(cancellationToken);
                    using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    tablesExist = await reader.ReadAsync(cancellationToken);
                }
                else
                {
                    // Check if GitHubDiscussions table exists in SQL Server
                    using var command = context.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GitHubDiscussions'";
                    await context.Database.OpenConnectionAsync(cancellationToken);
                    using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    tablesExist = await reader.ReadAsync(cancellationToken);
                }
            }
            catch
            {
                tablesExist = false;
            }

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

            _logger.LogInformation("Applied migrations: {AppliedCount} - {AppliedMigrations}", appliedMigrations.Count(), string.Join(", ", appliedMigrations));
            _logger.LogInformation("Pending migrations: {PendingCount}", pendingMigrations.Count());

            // If tables exist but migrations history says they don't, sync the history
            if (tablesExist && pendingMigrations.Any())
            {
                _logger.LogInformation("Tables already exist but migrations history is incomplete. Syncing migration history...");

                // Remove old migration records
                var oldMigrationId = "20251011072916_InitialGitHubDatabase";
                try
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM __EFMigrationsHistory WHERE MigrationId = {0}",
                        oldMigrationId);
                    _logger.LogInformation("Removed old migration record: {MigrationId}", oldMigrationId);
                }
                catch
                {
                    // Ignore if it doesn't exist
                }

                // Mark all pending migrations as applied
                foreach (var migration in pendingMigrations)
                {
                    try
                    {
                        var productVersion = typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly
                            .GetName().Version?.ToString() ?? "9.0.0";
                        await context.Database.ExecuteSqlRawAsync(
                            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                            migration, productVersion);
                        _logger.LogInformation("Marked migration as applied: {MigrationId}", migration);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to mark migration as applied: {MigrationId}", migration);
                    }
                }
            }
            else if (pendingMigrations.Any())
            {
                // Tables don't exist, run migrations normally
                _logger.LogInformation("Applying pending migrations: {Migrations}", string.Join(", ", pendingMigrations));
                await context.Database.MigrateAsync(cancellationToken);
            }

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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.IO;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class DatabaseMigrationHostedService : IHostedService
{
    private readonly IDbContextFactory<GitHubDbContext> _contextFactory;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IIOHelper _ioHelper;

    public DatabaseMigrationHostedService(
        IDbContextFactory<GitHubDbContext> contextFactory,
        ILogger<DatabaseMigrationHostedService> logger,
        IConfiguration configuration,
        IIOHelper ioHelper)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _configuration = configuration;
        _ioHelper = ioHelper;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Applying GitHub database migrations...");
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var connectionString = context.Database.GetConnectionString();
            var isSqlite = context.Database.IsSqlite();
            _logger.LogInformation("Database provider: {Provider}, Connection: {ConnectionString}",
                isSqlite ? "SQLite" : "SQL Server", connectionString);

            // Ensure the directory exists for SQLite database
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

            // Apply pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date, no pending migrations");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply GitHub database migrations");
            // Don't throw - let the app start, but log the error
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

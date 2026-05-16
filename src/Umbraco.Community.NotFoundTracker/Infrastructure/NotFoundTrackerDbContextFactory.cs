using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Design-time factory used by EF Core CLI (`dotnet ef migrations add`).
/// Provides a standalone SQLite configuration sufficient for scaffolding migrations;
/// the runtime configuration in the composer reads the real Umbraco connection string.
/// </summary>
public class NotFoundTrackerDbContextFactory : IDesignTimeDbContextFactory<NotFoundTrackerDbContext>
{
    public NotFoundTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotFoundTrackerDbContext>();
        optionsBuilder.UseSqlite("Data Source=not-found-tracker-design-time.db", sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("Umbraco.Community.NotFoundTracker");
        });
        return new NotFoundTrackerDbContext(optionsBuilder.Options);
    }
}

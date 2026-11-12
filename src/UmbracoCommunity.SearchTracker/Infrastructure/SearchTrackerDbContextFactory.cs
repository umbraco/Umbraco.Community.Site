using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UmbracoCommunity.SearchTracker.Infrastructure;

/// <summary>Design-time factory for <c>dotnet ef migrations add</c>.</summary>
public class SearchTrackerDbContextFactory : IDesignTimeDbContextFactory<SearchTrackerDbContext>
{
    public SearchTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SearchTrackerDbContext>();
        optionsBuilder.UseSqlite("Data Source=search-tracker-design-time.db", sqlite =>
            sqlite.MigrationsAssembly("UmbracoCommunity.SearchTracker"));
        return new SearchTrackerDbContext(optionsBuilder.Options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UmbracoCommunity.BlogAnnouncements.Infrastructure;

/// <summary>
/// Design-time factory used by EF Core CLI (`dotnet ef migrations add`). Provides a standalone
/// SQLite configuration sufficient for scaffolding migrations; the runtime configuration in the
/// composer reads the real Umbraco connection string.
/// </summary>
public class BlogAnnouncementsDbContextFactory : IDesignTimeDbContextFactory<BlogAnnouncementsDbContext>
{
    public BlogAnnouncementsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BlogAnnouncementsDbContext>();
        optionsBuilder.UseSqlite("Data Source=blog-announcements-design-time.db", sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("UmbracoCommunity.BlogAnnouncements");
        });
        return new BlogAnnouncementsDbContext(optionsBuilder.Options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

/// <summary>
/// Design-time factory used by EF Core CLI (`dotnet ef migrations add`).
/// Provides a standalone SQLite configuration sufficient for scaffolding migrations;
/// the runtime configuration in <see cref="RegisterMemberProfiles"/> reads the real
/// Umbraco connection string.
/// </summary>
public class MemberProfilesDbContextFactory : IDesignTimeDbContextFactory<MemberProfilesDbContext>
{
    public MemberProfilesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MemberProfilesDbContext>();
        optionsBuilder.UseSqlite("Data Source=member-profiles-design-time.db", sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("UmbracoCommunity.Web");
        });
        return new MemberProfilesDbContext(optionsBuilder.Options);
    }
}

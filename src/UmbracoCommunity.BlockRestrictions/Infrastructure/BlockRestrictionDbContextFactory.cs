using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// Design-time factory used by EF Core CLI tools (e.g. <c>dotnet ef migrations add</c>).
/// The CLI tools can't resolve the runtime DbContext configuration (which depends on
/// appsettings.json and the DI container), so this factory provides a standalone SQLite
/// configuration that's sufficient for generating migration code.
///
/// Usage:
///   cd src/UmbracoCommunity.BlockRestrictions
///   dotnet ef migrations add MigrationName
/// </summary>
public class BlockRestrictionDbContextFactory : IDesignTimeDbContextFactory<BlockRestrictionDbContext>
{
    public BlockRestrictionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BlockRestrictionDbContext>();

        // Use a throwaway SQLite database — this file is never actually created,
        // it's just needed to satisfy the SQLite provider during migration scaffolding.
        optionsBuilder.UseSqlite("Data Source=block-restrictions-design-time.db", sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("UmbracoCommunity.BlockRestrictions");
        });

        return new BlockRestrictionDbContext(optionsBuilder.Options);
    }
}

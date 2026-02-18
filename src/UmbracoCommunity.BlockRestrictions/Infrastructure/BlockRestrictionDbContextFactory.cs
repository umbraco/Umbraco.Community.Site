using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// Design-time factory for EF Core CLI tools (dotnet ef migrations add, etc.).
/// Uses SQLite by default for migration generation.
/// </summary>
public class BlockRestrictionDbContextFactory : IDesignTimeDbContextFactory<BlockRestrictionDbContext>
{
    public BlockRestrictionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BlockRestrictionDbContext>();
        optionsBuilder.UseSqlite("Data Source=block-restrictions-design-time.db", sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("UmbracoCommunity.BlockRestrictions");
        });

        return new BlockRestrictionDbContext(optionsBuilder.Options);
    }
}

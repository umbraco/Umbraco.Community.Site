using Microsoft.EntityFrameworkCore;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

/// <summary>
/// EF Core DbContext for block restriction rules.
/// This context shares the Umbraco database (via the umbracoDbDSN connection string)
/// but manages its own table (<c>BlockRestrictionRules</c>) independently.
/// Configured as a DbContext factory in <see cref="BlockRestrictionComposer"/> to support
/// both SQLite (local dev) and SQL Server (production) providers.
/// </summary>
public class BlockRestrictionDbContext : DbContext
{
    public BlockRestrictionDbContext(DbContextOptions<BlockRestrictionDbContext> options)
        : base(options)
    {
    }

    /// <summary>The block restriction rules table.</summary>
    public DbSet<BlockRestrictionEntity> BlockRestrictionRules { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlockRestrictionEntity>(entity =>
        {
            entity.ToTable("BlockRestrictionRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentTypeKey).IsRequired();
            entity.Property(e => e.AllowedBlockAliasesJson).IsRequired();

            // One rule per document type — prevents duplicate rules and enables
            // efficient lookup by document type key during content tree walks.
            entity.HasIndex(e => e.DocumentTypeKey).IsUnique();
        });
    }
}

using Microsoft.EntityFrameworkCore;

namespace UmbracoCommunity.BlockRestrictions.Infrastructure;

public class BlockRestrictionDbContext : DbContext
{
    public BlockRestrictionDbContext(DbContextOptions<BlockRestrictionDbContext> options)
        : base(options)
    {
    }

    public DbSet<BlockRestrictionEntity> BlockRestrictionRules { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlockRestrictionEntity>(entity =>
        {
            entity.ToTable("BlockRestrictionRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentTypeKey).IsRequired();
            entity.Property(e => e.AllowedBlockAliasesJson).IsRequired();
            entity.HasIndex(e => e.DocumentTypeKey).IsUnique();
        });
    }
}

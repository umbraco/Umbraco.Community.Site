using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

public class NotFoundTrackerDbContext : DbContext
{
    public NotFoundTrackerDbContext(DbContextOptions<NotFoundTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotFoundHitEntity> NotFoundHits { get; set; } = null!;
    public DbSet<NotFoundHitQueryStringEntity> NotFoundHitQueryStrings { get; set; } = null!;
    public DbSet<NotFoundIgnoreRuleEntity> NotFoundIgnoreRules { get; set; } = null!;
    public DbSet<NotFoundPresetSeedRecordEntity> NotFoundPresetSeedRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotFoundHitEntity>(entity =>
        {
            entity.ToTable("NotFoundHits");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Hostname).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.LastUserAgent).HasMaxLength(512);

            entity.HasIndex(e => e.Hostname);
            entity.HasIndex(e => e.LastSeenUtc);
            entity.HasIndex(e => e.HitCount);

            entity.HasMany(e => e.QueryStrings)
                .WithOne(qs => qs.Hit!)
                .HasForeignKey(qs => qs.HitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotFoundHitQueryStringEntity>(entity =>
        {
            entity.ToTable("NotFoundHitQueryStrings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.QueryString).IsRequired().HasMaxLength(2048);

            entity.HasIndex(e => e.HitId);
            entity.HasIndex(e => e.LastSeenUtc);
        });

        modelBuilder.Entity<NotFoundIgnoreRuleEntity>(entity =>
        {
            entity.ToTable("NotFoundIgnoreRules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Hostname).HasMaxLength(255);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasIndex(e => e.Hostname);
        });

        modelBuilder.Entity<NotFoundPresetSeedRecordEntity>(entity =>
        {
            entity.ToTable("NotFoundPresetSeedRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Hostname).HasMaxLength(255);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(2048);

            entity.HasIndex(e => e.Hostname);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

public class MemberProfilesDbContext : DbContext
{
    public MemberProfilesDbContext(DbContextOptions<MemberProfilesDbContext> options)
        : base(options)
    {
    }

    public DbSet<MemberProfileEntity> MemberProfiles { get; set; } = null!;
    public DbSet<MemberFeedEntity> MemberFeeds { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemberProfileEntity>(entity =>
        {
            entity.ToTable("MemberProfiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.GitHubHandle).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Bio).HasMaxLength(1000);
            entity.Property(e => e.PlatformProfileId).HasMaxLength(100);

            entity.HasIndex(e => e.MemberKey).IsUnique();
            entity.HasIndex(e => e.GitHubHandle);

            entity.HasMany(e => e.Feeds)
                .WithOne(f => f.MemberProfile!)
                .HasForeignKey(f => f.MemberProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemberFeedEntity>(entity =>
        {
            entity.ToTable("MemberFeeds");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Platform).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.RemovedReason).HasMaxLength(500);
            entity.Property(e => e.LastPlatformSyncError).HasMaxLength(1000);

            entity.HasIndex(e => e.MemberProfileId);
        });
    }
}

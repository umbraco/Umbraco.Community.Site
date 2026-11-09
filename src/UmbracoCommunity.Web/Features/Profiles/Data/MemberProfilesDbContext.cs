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
    public DbSet<ProfileMatchEntity> ProfileMatches { get; set; } = null!;
    public DbSet<MemberPinnedItemEntity> MemberPinnedItems { get; set; } = null!;
    public DbSet<MemberActivityEntryEntity> MemberActivityEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemberProfileEntity>(entity =>
        {
            entity.ToTable("MemberProfiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.GitHubHandle).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DisplayNameOverride).HasMaxLength(200);
            entity.Property(e => e.Headline).HasMaxLength(200);
            entity.Property(e => e.Bio).HasMaxLength(1000);
            entity.Property(e => e.GitHubBio).HasMaxLength(1000);
            entity.Property(e => e.GitHubCompany).HasMaxLength(200);
            entity.Property(e => e.PlatformProfileId).HasMaxLength(100);

            entity.HasIndex(e => e.MemberKey).IsUnique();
            entity.HasIndex(e => e.GitHubHandle);

            entity.HasMany(e => e.Feeds)
                .WithOne(f => f.MemberProfile!)
                .HasForeignKey(f => f.MemberProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.PinnedItems)
                .WithOne(p => p.MemberProfile!)
                .HasForeignKey(p => p.MemberProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemberPinnedItemEntity>(entity =>
        {
            entity.ToTable("MemberPinnedItems");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ActivityId).IsRequired().HasMaxLength(ProfileDisplay.MaxActivityIdLength);
            entity.Property(e => e.Reason).HasMaxLength(ProfileDisplay.MaxPinReasonLength);
            entity.Property(e => e.Group).HasMaxLength(MemberPinnedItemEntity.MaxGroupLength);
            entity.Property(e => e.Title).HasMaxLength(MemberPinnedItemEntity.MaxTitleLength);
            entity.Property(e => e.Url).HasMaxLength(MemberPinnedItemEntity.MaxUrlLength);
            entity.Property(e => e.Description).HasMaxLength(MemberPinnedItemEntity.MaxDescriptionLength);
            entity.Property(e => e.Source).HasMaxLength(MemberPinnedItemEntity.MaxSourceLength);

            entity.HasIndex(e => e.MemberProfileId);
        });

        modelBuilder.Entity<MemberActivityEntryEntity>(entity =>
        {
            entity.ToTable("MemberActivityEntries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Kind).IsRequired().HasMaxLength(MemberActivityEntryEntity.MaxKindLength);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(MemberActivityEntryEntity.MaxTitleLength);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(MemberActivityEntryEntity.MaxUrlLength);
            entity.Property(e => e.Description).HasMaxLength(MemberActivityEntryEntity.MaxDescriptionLength);
            entity.Property(e => e.RejectionReason).HasMaxLength(MemberActivityEntryEntity.MaxReasonLength);
            entity.Property(e => e.DecidedByUserName).HasMaxLength(200);

            entity.HasOne(e => e.MemberProfile)
                .WithMany()
                .HasForeignKey(e => e.MemberProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.MemberProfileId);
            entity.HasIndex(e => e.State);
        });

        modelBuilder.Entity<MemberFeedEntity>(entity =>
        {
            entity.ToTable("MemberFeeds");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Platform).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.RemovedReason).HasMaxLength(500);
            entity.Property(e => e.LastPlatformSyncError).HasMaxLength(1000);
            entity.Property(e => e.PreviewTitle).HasMaxLength(200);
            entity.Property(e => e.PreviewImageUrl).HasMaxLength(2048);
            entity.Property(e => e.FaviconUrl).HasMaxLength(2048);

            entity.HasIndex(e => e.MemberProfileId);
        });

        modelBuilder.Entity<ProfileMatchEntity>(entity =>
        {
            entity.ToTable("ProfileMatches");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SourceUrl).IsRequired().HasMaxLength(ProfileMatchEntity.MaxSourceUrlLength);
            entity.Property(e => e.SignalsJson).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.DecidedByUserName).HasMaxLength(200);

            entity.HasOne(e => e.MemberProfile)
                .WithMany()
                .HasForeignKey(e => e.MemberProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MemberProfileId, e.SourceUrl }).IsUnique();
            entity.HasIndex(e => e.State);
        });
    }
}

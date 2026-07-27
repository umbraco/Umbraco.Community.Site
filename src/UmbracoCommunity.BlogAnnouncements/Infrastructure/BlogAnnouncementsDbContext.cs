using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.BlogAnnouncements.Detection;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;

namespace UmbracoCommunity.BlogAnnouncements.Infrastructure;

/// <summary>
/// Tracking store for the Discord blog-announcement pipeline. Mirrors the NotFoundTracker
/// pattern: provider-agnostic (SQLite / SQL Server), migrations in this assembly.
/// </summary>
public class BlogAnnouncementsDbContext : DbContext
{
    public BlogAnnouncementsDbContext(DbContextOptions<BlogAnnouncementsDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnnouncedBlogPost> AnnouncedBlogPosts { get; set; } = null!;
    public DbSet<AnnouncementAttempt> AnnouncementAttempts { get; set; } = null!;
    public DbSet<AnnouncementRun> AnnouncementRuns { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnnouncedBlogPost>(entity =>
        {
            entity.ToTable("AnnouncedBlogPosts");
            entity.HasKey(e => e.PlatformPostId);
            entity.Property(e => e.PlatformPostId).ValueGeneratedNever();

            entity.Property(e => e.Url).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(1024);
            // 450 keeps the indexed column inside SQL Server's index key size limits
            // (900 bytes for classic nonclustered keys; nvarchar doubles the byte count).
            entity.Property(e => e.Fingerprint).IsRequired().HasMaxLength(AnnouncementFingerprint.MaxLength);
            entity.Property(e => e.AuthorName).HasMaxLength(512);
            entity.Property(e => e.AuthorAvatarUrl).HasMaxLength(2048);
            entity.Property(e => e.AuthorProfileUrl).HasMaxLength(2048);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(2048);

            entity.HasIndex(e => e.Fingerprint);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PublishedAtUtc);

            entity.HasMany(e => e.Attempts)
                .WithOne(a => a.Post!)
                .HasForeignKey(a => a.PlatformPostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnnouncementAttempt>(entity =>
        {
            entity.ToTable("AnnouncementAttempts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Outcome).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Destination).IsRequired().HasMaxLength(64).HasDefaultValue("Discord");

            entity.HasIndex(e => e.PlatformPostId);
            entity.HasIndex(e => e.AttemptedUtc);
        });

        modelBuilder.Entity<AnnouncementRun>(entity =>
        {
            entity.ToTable("AnnouncementRuns");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.RunUtc);
        });
    }
}

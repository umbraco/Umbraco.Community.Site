using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure.Entities;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubDbContext : DbContext
{
    public GitHubDbContext(DbContextOptions<GitHubDbContext> options) : base(options)
    {
    }

    public DbSet<PullRequestEntity> PullRequests => Set<PullRequestEntity>();
    public DbSet<IssueEntity> Issues => Set<IssueEntity>();
    public DbSet<DiscussionEntity> Discussions => Set<DiscussionEntity>();
    public DbSet<HqMemberEntity> HqMembers => Set<HqMemberEntity>();
    public DbSet<PullRequestReleaseEntity> PullRequestReleases => Set<PullRequestReleaseEntity>();
    public DbSet<IssueReleaseEntity> IssueReleases => Set<IssueReleaseEntity>();
    public DbSet<NuGetPackageVersionEntity> NuGetPackageVersions => Set<NuGetPackageVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PullRequest configuration
        modelBuilder.Entity<PullRequestEntity>(entity =>
        {
            entity.ToTable("GitHubPullRequests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.RepositoryName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Number).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Data).IsRequired();

            entity.HasIndex(e => new { e.RepositoryName, e.Number }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        // Issue configuration
        modelBuilder.Entity<IssueEntity>(entity =>
        {
            entity.ToTable("GitHubIssues");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.RepositoryName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Number).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Data).IsRequired();

            entity.HasIndex(e => new { e.RepositoryName, e.Number }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        // Discussion configuration
        modelBuilder.Entity<DiscussionEntity>(entity =>
        {
            entity.ToTable("GitHubDiscussions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.RepositoryName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Number).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Data).IsRequired();

            entity.HasIndex(e => new { e.RepositoryName, e.Number }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        // HqMember configuration
        modelBuilder.Entity<HqMemberEntity>(entity =>
        {
            entity.ToTable("GitHubHqMembers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.Login).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Data).IsRequired();

            entity.HasIndex(e => e.Login).IsUnique();
        });

        // PullRequestRelease junction table
        modelBuilder.Entity<PullRequestReleaseEntity>(entity =>
        {
            entity.ToTable("GitHubPullRequestReleases");
            entity.HasKey(e => new { e.PullRequestId, e.ReleaseLabel });
            entity.Property(e => e.PullRequestId).HasMaxLength(100);
            entity.Property(e => e.ReleaseLabel).HasMaxLength(50);

            entity.HasOne<PullRequestEntity>()
                .WithMany()
                .HasForeignKey(e => e.PullRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ReleaseLabel);
        });

        // IssueRelease junction table
        modelBuilder.Entity<IssueReleaseEntity>(entity =>
        {
            entity.ToTable("GitHubIssueReleases");
            entity.HasKey(e => new { e.IssueId, e.ReleaseLabel });
            entity.Property(e => e.IssueId).HasMaxLength(100);
            entity.Property(e => e.ReleaseLabel).HasMaxLength(50);

            entity.HasOne<IssueEntity>()
                .WithMany()
                .HasForeignKey(e => e.IssueId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ReleaseLabel);
        });

        // NuGetPackageVersion configuration
        modelBuilder.Entity<NuGetPackageVersionEntity>(entity =>
        {
            entity.ToTable("NuGetPackageVersions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PackageId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PublishedDate).IsRequired();
            entity.Property(e => e.LastSyncedAt).IsRequired();

            entity.HasIndex(e => new { e.PackageId, e.Version }).IsUnique();
            entity.HasIndex(e => e.PackageId);
        });
    }
}

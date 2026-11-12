using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.SearchTracker.Models.Entities;

namespace UmbracoCommunity.SearchTracker.Infrastructure;

public class SearchTrackerDbContext : DbContext
{
    public const int TermMaxLength = 200;

    public SearchTrackerDbContext(DbContextOptions<SearchTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<SearchTermDailyEntity> SearchTermDaily { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchTermDailyEntity>(entity =>
        {
            entity.ToTable("SearchTrackerTermDaily");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Term).IsRequired().HasMaxLength(TermMaxLength);

            entity.HasIndex(e => new { e.SiteKey, e.Day, e.Term }).IsUnique();
            entity.HasIndex(e => e.Day);
            entity.HasIndex(e => new { e.Term, e.Day });
        });
    }
}

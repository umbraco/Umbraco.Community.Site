using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundHitServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public NotFoundHitServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);
    private NotFoundHitService BuildService() => new(new TestFactory(_dbOptions));

    private static UserScope FullAccess() => new(new HashSet<string>(), hasFullAccess: true);
    private static UserScope Scoped(params string[] hosts)
        => new(new HashSet<string>(hosts, StringComparer.Ordinal), hasFullAccess: false);

    [Fact]
    public async Task List_filters_by_scope_when_not_full_access()
    {
        await Seed(("a.example", "/foo"), ("b.example", "/bar"));

        var (items, total) = await BuildService().ListAsync(new HitListQuery(), Scoped("a.example"), default);

        items.Should().HaveCount(1);
        items[0].Hit.Hostname.Should().Be("a.example");
        total.Should().Be(1);
    }

    [Fact]
    public async Task List_with_full_access_returns_all()
    {
        await Seed(("a.example", "/foo"), ("b.example", "/bar"));

        var (items, total) = await BuildService().ListAsync(new HitListQuery(), FullAccess(), default);

        items.Should().HaveCount(2);
        total.Should().Be(2);
    }

    [Fact]
    public async Task List_hostname_filter_is_intersected_with_scope()
    {
        await Seed(("a.example", "/foo"), ("b.example", "/bar"));

        var (items, _) = await BuildService().ListAsync(
            new HitListQuery { Hostname = "b.example" },
            Scoped("a.example"),
            default);

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task List_sort_by_popularity_orders_descending_by_hit_count()
    {
        await SeedOne(("a.example", "/low",  hitCount: 1L));
        await SeedOne(("a.example", "/high", hitCount: 100L));
        await SeedOne(("a.example", "/mid",  hitCount: 50L));

        var (items, _) = await BuildService().ListAsync(
            new HitListQuery { Sort = HitSort.Popularity },
            FullAccess(),
            default);

        items.Select(i => i.Hit.Path).Should().ContainInOrder("/high", "/mid", "/low");
    }

    [Fact]
    public async Task List_sort_by_recently_seen_orders_descending_by_last_seen()
    {
        var now = DateTime.UtcNow;
        await SeedOne(("a.example", "/old",    lastSeenUtc: now.AddDays(-10)));
        await SeedOne(("a.example", "/recent", lastSeenUtc: now.AddDays(-1)));

        var (items, _) = await BuildService().ListAsync(
            new HitListQuery { Sort = HitSort.RecentlySeen },
            FullAccess(),
            default);

        items.Select(i => i.Hit.Path).Should().ContainInOrder("/recent", "/old");
    }

    [Fact]
    public async Task List_status_filter_defaults_to_active()
    {
        await SeedOne(("a.example", "/active",     status: HitStatus.Active));
        await SeedOne(("a.example", "/redirected", status: HitStatus.Redirected));

        var (items, _) = await BuildService().ListAsync(new HitListQuery(), FullAccess(), default);

        items.Should().ContainSingle(i => i.Hit.Path == "/active");
    }

    [Fact]
    public async Task List_paginates_correctly()
    {
        for (var i = 0; i < 30; i++)
        {
            await SeedOne(("a.example", $"/p{i:D2}", hitCount: (long)(30 - i)));
        }

        var (items, total) = await BuildService().ListAsync(
            new HitListQuery { Sort = HitSort.Popularity, Skip = 10, Take = 5 },
            FullAccess(),
            default);

        items.Should().HaveCount(5);
        total.Should().Be(30);
        items[0].Hit.Path.Should().Be("/p10");
    }

    [Fact]
    public async Task Get_returns_null_when_row_outside_scope()
    {
        await Seed(("a.example", "/foo"));
        var id = (await Ctx().NotFoundHits.SingleAsync()).Id;

        var hit = await BuildService().GetAsync(id, Scoped("b.example"), default);

        hit.Should().BeNull();
    }

    [Fact]
    public async Task Delete_returns_false_when_row_outside_scope()
    {
        await Seed(("a.example", "/foo"));
        var id = (await Ctx().NotFoundHits.SingleAsync()).Id;

        var ok = await BuildService().DeleteAsync(id, Scoped("b.example"), default);

        ok.Should().BeFalse();
        (await Ctx().NotFoundHits.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task BulkDelete_reports_processed_and_skipped()
    {
        await Seed(("a.example", "/foo"), ("b.example", "/bar"));
        var ids = await Ctx().NotFoundHits.Select(h => h.Id).ToListAsync();

        var (processed, skipped) = await BuildService().BulkDeleteAsync(ids, Scoped("a.example"), default);

        processed.Should().Be(1);
        skipped.Should().Be(1);
    }

    [Fact]
    public async Task Distinct_hostnames_intersects_with_scope()
    {
        await Seed(("a.example", "/x"), ("b.example", "/x"), ("c.example", "/x"));

        var hosts = await BuildService().GetDistinctHostnamesAsync(Scoped("a.example", "c.example"), default);

        hosts.Should().BeEquivalentTo(new[] { "a.example", "c.example" });
    }

    private async Task Seed(params (string Hostname, string Path)[] rows)
    {
        using var ctx = Ctx();
        foreach (var (h, p) in rows)
        {
            ctx.NotFoundHits.Add(new NotFoundHitEntity
            {
                Hostname = h, Path = p, HitCount = 1,
                FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow,
                Status = HitStatus.Active,
            });
        }
        await ctx.SaveChangesAsync();
    }

    private async Task SeedOne(
        (string Hostname, string Path, long hitCount, DateTime lastSeenUtc, HitStatus status) row)
    {
        using var ctx = Ctx();
        ctx.NotFoundHits.Add(new NotFoundHitEntity
        {
            Hostname = row.Hostname, Path = row.Path, HitCount = row.hitCount,
            FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = row.lastSeenUtc, Status = row.status,
        });
        await ctx.SaveChangesAsync();
    }

    private Task SeedOne((string Hostname, string Path, long hitCount) row)
        => SeedOne((row.Hostname, row.Path, row.hitCount, DateTime.UtcNow, HitStatus.Active));

    private Task SeedOne((string Hostname, string Path, DateTime lastSeenUtc) row)
        => SeedOne((row.Hostname, row.Path, 1L, row.lastSeenUtc, HitStatus.Active));

    private Task SeedOne((string Hostname, string Path, HitStatus status) row)
        => SeedOne((row.Hostname, row.Path, 1L, DateTime.UtcNow, row.status));

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}

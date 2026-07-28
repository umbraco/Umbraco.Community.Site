using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundRetentionServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public NotFoundRetentionServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);

    private NotFoundRetentionService BuildService(NotFoundTrackerOptions opts)
    {
        var factory = new TestFactory(_dbOptions);
        return new NotFoundRetentionService(
            factory,
            new OptionsWrapper<NotFoundTrackerOptions>(opts),
            NullLogger<NotFoundRetentionService>.Instance);
    }

    [Fact]
    public async Task Sweep_deletes_active_hits_older_than_active_retention()
    {
        var now = DateTime.UtcNow;
        using (var ctx = Ctx())
        {
            ctx.NotFoundHits.AddRange(
                new NotFoundHitEntity { Hostname = "a", Path = "/old", LastSeenUtc = now.AddDays(-91), FirstSeenUtc = now.AddDays(-100), Status = HitStatus.Active },
                new NotFoundHitEntity { Hostname = "a", Path = "/new", LastSeenUtc = now.AddDays(-30), FirstSeenUtc = now.AddDays(-30), Status = HitStatus.Active });
            await ctx.SaveChangesAsync();
        }

        var service = BuildService(new NotFoundTrackerOptions { ActiveRetentionDays = 90 });
        await service.SweepAsync(CancellationToken.None);

        using var verify = Ctx();
        var paths = await verify.NotFoundHits.Select(h => h.Path).ToListAsync();
        paths.Should().BeEquivalentTo(["/new"]);
    }

    [Fact]
    public async Task Sweep_deletes_actioned_hits_older_than_actioned_retention()
    {
        var now = DateTime.UtcNow;
        using (var ctx = Ctx())
        {
            ctx.NotFoundHits.AddRange(
                new NotFoundHitEntity { Hostname = "a", Path = "/redirected-old", LastSeenUtc = now.AddDays(-8), FirstSeenUtc = now.AddDays(-30), Status = HitStatus.Redirected },
                new NotFoundHitEntity { Hostname = "a", Path = "/ignored-old",   LastSeenUtc = now.AddDays(-8), FirstSeenUtc = now.AddDays(-30), Status = HitStatus.IgnoredManually },
                new NotFoundHitEntity { Hostname = "a", Path = "/redirected-new", LastSeenUtc = now.AddDays(-2), FirstSeenUtc = now.AddDays(-30), Status = HitStatus.Redirected });
            await ctx.SaveChangesAsync();
        }

        var service = BuildService(new NotFoundTrackerOptions { ActionedRetentionDays = 7 });
        await service.SweepAsync(CancellationToken.None);

        using var verify = Ctx();
        var paths = await verify.NotFoundHits.Select(h => h.Path).ToListAsync();
        paths.Should().BeEquivalentTo(["/redirected-new"]);
    }

    [Fact]
    public async Task Sweep_deletes_query_string_rows_older_than_qs_retention()
    {
        var now = DateTime.UtcNow;
        using (var ctx = Ctx())
        {
            var hit = new NotFoundHitEntity
            {
                Hostname = "a",
                Path = "/foo",
                LastSeenUtc = now.AddDays(-3),
                FirstSeenUtc = now.AddDays(-30),
                Status = HitStatus.Active,
                QueryStrings = new List<NotFoundHitQueryStringEntity>
                {
                    new() { QueryString = "?a=1", LastSeenUtc = now.AddDays(-15) },
                    new() { QueryString = "?b=2", LastSeenUtc = now.AddDays(-1)  },
                }
            };
            ctx.NotFoundHits.Add(hit);
            await ctx.SaveChangesAsync();
        }

        var service = BuildService(new NotFoundTrackerOptions { QueryStringRetentionDays = 14 });
        await service.SweepAsync(CancellationToken.None);

        using var verify = Ctx();
        var qs = await verify.NotFoundHitQueryStrings.Select(q => q.QueryString).ToListAsync();
        qs.Should().BeEquivalentTo(["?b=2"]);
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}

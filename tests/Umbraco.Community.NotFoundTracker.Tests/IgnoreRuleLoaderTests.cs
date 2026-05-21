using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class IgnoreRuleLoaderTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public IgnoreRuleLoaderTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);

    private IgnoreRuleLoader BuildLoader() => new(new TestFactory(_dbOptions));

    [Fact]
    public async Task Empty_table_produces_empty_snapshot()
    {
        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        snapshot.Global.ExactPaths.Should().BeEmpty();
        snapshot.ByHostname.Should().BeEmpty();
    }

    [Fact]
    public async Task Null_hostname_rules_go_into_Global_bucket()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.Exact, Path = "/wp-login.php",
            });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.PathPrefix, Path = "/wp-admin",
            });
            await ctx.SaveChangesAsync();
        }

        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        snapshot.Global.ExactPaths.Should().Contain("/wp-login.php");
        snapshot.Global.PrefixPaths.Matches("/wp-admin").Should().BeTrue();
        snapshot.Global.PrefixPaths.Matches("/wp-admin/login").Should().BeTrue();
        snapshot.ByHostname.Should().BeEmpty();
    }

    [Fact]
    public async Task Hostname_scoped_rules_go_into_per_hostname_buckets()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "site-a.example", MatchType = IgnoreMatchType.Exact, Path = "/old-page",
            });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "site-b.example", MatchType = IgnoreMatchType.PathPrefix, Path = "/legacy",
            });
            await ctx.SaveChangesAsync();
        }

        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        snapshot.ByHostname.Should().HaveCount(2);
        snapshot.ByHostname["site-a.example"].ExactPaths.Should().Contain("/old-page");
        snapshot.ByHostname["site-b.example"].PrefixPaths.Matches("/legacy/old").Should().BeTrue();
    }

    [Fact]
    public async Task Hostnames_are_keyed_case_insensitively()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "Example.COM", MatchType = IgnoreMatchType.Exact, Path = "/foo",
            });
            await ctx.SaveChangesAsync();
        }

        var snapshot = await BuildLoader().LoadAsync(CancellationToken.None);

        // Hostname stored is lowercased on the snapshot side so lookups by lowercased
        // request hostname find the bucket.
        snapshot.ByHostname.Should().ContainKey("example.com");
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}

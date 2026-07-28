using Microsoft.EntityFrameworkCore;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class IgnoreRuleMatcherTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public IgnoreRuleMatcherTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);

    private IgnoreRuleMatcher BuildMatcher() => new(new IgnoreRuleLoader(new TestFactory(_dbOptions)));

    [Fact]
    public void Without_refresh_an_empty_matcher_ignores_nothing()
    {
        var matcher = BuildMatcher();
        matcher.IsIgnored("example.com", "/wp-admin").Should().BeFalse();
    }

    [Fact]
    public async Task After_refresh_global_prefix_rule_matches_any_hostname()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.PathPrefix, Path = "/wp-admin",
            });
            await ctx.SaveChangesAsync();
        }
        var matcher = BuildMatcher();
        await matcher.RefreshAsync();

        matcher.IsIgnored("site-a.example", "/wp-admin").Should().BeTrue();
        matcher.IsIgnored("site-b.example", "/wp-admin/login").Should().BeTrue();
        matcher.IsIgnored("any-host", "/legit-page").Should().BeFalse();
    }

    [Fact]
    public async Task Hostname_scoped_rule_only_matches_that_hostname()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = "site-a.example",
                MatchType = IgnoreMatchType.Exact,
                Path = "/old-page",
            });
            await ctx.SaveChangesAsync();
        }
        var matcher = BuildMatcher();
        await matcher.RefreshAsync();

        matcher.IsIgnored("site-a.example", "/old-page").Should().BeTrue();
        matcher.IsIgnored("site-b.example", "/old-page").Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_swaps_the_snapshot_atomically()
    {
        var matcher = BuildMatcher();
        await matcher.RefreshAsync();
        matcher.IsIgnored("example.com", "/foo").Should().BeFalse();

        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, MatchType = IgnoreMatchType.Exact, Path = "/foo",
            });
            await ctx.SaveChangesAsync();
        }

        // Without refresh the matcher still sees the old (empty) snapshot.
        matcher.IsIgnored("example.com", "/foo").Should().BeFalse();

        await matcher.RefreshAsync();
        matcher.IsIgnored("example.com", "/foo").Should().BeTrue();
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}

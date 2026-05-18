using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class AutoPresetSeedingServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;

    public AutoPresetSeedingServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);

    private (AutoPresetSeedingService service, IgnoreRuleMatcher matcher) Build(NotFoundTrackerOptions opts)
    {
        var factory = new TestFactory(_dbOptions);
        var loader = new IgnoreRuleLoader(factory);
        var matcher = new IgnoreRuleMatcher(loader);
        var service = new AutoPresetSeedingService(
            factory,
            matcher,
            new OptionsWrapper<NotFoundTrackerOptions>(opts),
            NullLogger<AutoPresetSeedingService>.Instance);
        return (service, matcher);
    }

    [Fact]
    public async Task Seeds_all_built_in_preset_entries_when_table_is_empty()
    {
        var (service, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });

        await service.StartAsync(CancellationToken.None);

        using var verify = Ctx();
        var rows = await verify.NotFoundIgnoreRules.ToListAsync();
        rows.Should().HaveCount(DefaultIgnoreRules.All.Count);
        rows.Should().OnlyContain(r => r.Source == IgnoreRuleSource.AutoPreset);
        rows.Should().Contain(r => r.Path == "/wp-admin" && r.MatchType == IgnoreMatchType.PathPrefix);
    }

    [Fact]
    public async Task Does_not_re_insert_already_present_preset_entries()
    {
        // Seed once.
        var (service1, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service1.StartAsync(CancellationToken.None);

        var initialCount = await Ctx().NotFoundIgnoreRules.CountAsync();

        // Seed again — same defaults, should be no-ops.
        var (service2, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service2.StartAsync(CancellationToken.None);

        (await Ctx().NotFoundIgnoreRules.CountAsync()).Should().Be(initialCount);
    }

    [Fact]
    public async Task Editor_deleted_AutoPreset_entries_are_not_re_inserted()
    {
        // Seed first run.
        var (service1, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service1.StartAsync(CancellationToken.None);

        // Editor deletes /wp-admin from the dashboard.
        using (var ctx = Ctx())
        {
            var wpAdmin = await ctx.NotFoundIgnoreRules.FirstAsync(r => r.Path == "/wp-admin");
            ctx.NotFoundIgnoreRules.Remove(wpAdmin);
            await ctx.SaveChangesAsync();
        }

        // Restart: seeding runs again. /wp-admin must stay deleted.
        var (service2, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });
        await service2.StartAsync(CancellationToken.None);

        using var verify = Ctx();
        (await verify.NotFoundIgnoreRules.AnyAsync(r => r.Path == "/wp-admin")).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAutoPreset_false_skips_built_in_preset()
    {
        var (service, _) = Build(new NotFoundTrackerOptions { SeedAutoPreset = false });

        await service.StartAsync(CancellationToken.None);

        (await Ctx().NotFoundIgnoreRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Refreshes_matcher_after_seeding()
    {
        var (service, matcher) = Build(new NotFoundTrackerOptions { SeedAutoPreset = true });

        await service.StartAsync(CancellationToken.None);

        // Built-in preset includes /wp-admin as a PathPrefix rule.
        matcher.IsIgnored("any-host", "/wp-admin").Should().BeTrue();
        matcher.IsIgnored("any-host", "/wp-admin/login").Should().BeTrue();
        matcher.IsIgnored("any-host", "/legit").Should().BeFalse();
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}

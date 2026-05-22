using Microsoft.EntityFrameworkCore;
using Moq;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundIgnoreRuleServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;
    private readonly Mock<INotFoundIgnoreRuleMatcher> _matcher = new();

    public NotFoundIgnoreRuleServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>().UseSqlite(_connection).Options;
        using var ctx = new NotFoundTrackerDbContext(_dbOptions);
        ctx.Database.EnsureCreated();
        _matcher.Setup(m => m.RefreshAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext Ctx() => new(_dbOptions);
    private NotFoundIgnoreRuleService Build() => new(new TestFactory(_dbOptions), _matcher.Object);
    private static UserScope FullAccess() => new(new HashSet<string>(), hasFullAccess: true);
    private static UserScope Scoped(params string[] hosts)
        => new(new HashSet<string>(hosts, StringComparer.Ordinal), hasFullAccess: false);

    [Fact]
    public async Task List_includes_global_rules_for_all_users()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity { Hostname = null, Path = "/x", MatchType = IgnoreMatchType.Exact, Source = IgnoreRuleSource.AutoPreset });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity { Hostname = "a", Path = "/y", MatchType = IgnoreMatchType.Exact, Source = IgnoreRuleSource.UserDefined });
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity { Hostname = "b", Path = "/z", MatchType = IgnoreMatchType.Exact, Source = IgnoreRuleSource.UserDefined });
            await ctx.SaveChangesAsync();
        }

        var list = await Build().ListAsync(Scoped("a"), default);

        list.Select(r => r.Path).Should().BeEquivalentTo(["/x", "/y"]);
    }

    [Fact]
    public async Task Create_normalizes_path_and_refreshes_matcher()
    {
        var result = await Build().CreateAsync(
            new CreateIgnoreRuleInput("/FOO", IgnoreMatchType.PathPrefix, Hostname: null, Note: "n"),
            FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Ok);
        result.Entity!.Path.Should().Be("/foo");
        result.Entity!.Source.Should().Be(IgnoreRuleSource.UserDefined);
        _matcher.Verify(m => m.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_returns_Conflict_on_duplicate_key()
    {
        await Build().CreateAsync(new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, null, null), FullAccess(), default);
        var result = await Build().CreateAsync(new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, null, null), FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Conflict);
    }

    [Fact]
    public async Task Create_global_rule_is_Forbidden_when_user_lacks_full_access()
    {
        var result = await Build().CreateAsync(
            new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, Hostname: null, Note: null),
            Scoped("a"), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Forbidden);
        (await Ctx().NotFoundIgnoreRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_hostname_rule_is_Forbidden_when_user_lacks_that_hostname()
    {
        var result = await Build().CreateAsync(
            new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, Hostname: "b", Note: null),
            Scoped("a"), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Forbidden);
    }

    [Fact]
    public async Task Update_ConfigSeeded_rule_is_Forbidden_for_everyone()
    {
        using (var ctx = Ctx())
        {
            ctx.NotFoundIgnoreRules.Add(new NotFoundIgnoreRuleEntity
            {
                Hostname = null, Path = "/foo", MatchType = IgnoreMatchType.Exact,
                Source = IgnoreRuleSource.ConfigSeeded,
            });
            await ctx.SaveChangesAsync();
        }
        var id = (await Ctx().NotFoundIgnoreRules.SingleAsync()).Id;

        var result = await Build().UpdateAsync(id, new UpdateIgnoreRuleInput("/bar", IgnoreMatchType.Exact, null, null), FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Forbidden);
        result.Reason.Should().Contain("ConfigSeeded");
    }

    [Fact]
    public async Task Delete_NotFound_when_no_such_id()
    {
        var result = await Build().DeleteAsync(9999, FullAccess(), default);
        result.Result.Should().Be(IgnoreRuleMutationResult.NotFound);
    }

    [Fact]
    public async Task Delete_refreshes_matcher_on_success()
    {
        await Build().CreateAsync(new CreateIgnoreRuleInput("/foo", IgnoreMatchType.Exact, null, null), FullAccess(), default);
        var id = (await Ctx().NotFoundIgnoreRules.SingleAsync()).Id;
        _matcher.Invocations.Clear();

        var result = await Build().DeleteAsync(id, FullAccess(), default);

        result.Result.Should().Be(IgnoreRuleMutationResult.Ok);
        _matcher.Verify(m => m.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}

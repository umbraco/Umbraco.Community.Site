using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UmbracoCommunity.SearchTracker.Infrastructure;
using UmbracoCommunity.SearchTracker.Models.Entities;

namespace UmbracoCommunity.SearchTracker.Tests;

public abstract class SqliteTestBase : IDisposable
{
    private readonly SqliteConnection _connection;

    protected SqliteTestBase()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        DbOptions = new DbContextOptionsBuilder<SearchTrackerDbContext>().UseSqlite(_connection).Options;
        using var context = new SearchTrackerDbContext(DbOptions);
        context.Database.EnsureCreated();
        Factory = new TestContextFactory(DbOptions);
    }

    protected DbContextOptions<SearchTrackerDbContext> DbOptions { get; }
    protected IDbContextFactory<SearchTrackerDbContext> Factory { get; }

    protected SearchTrackerDbContext CreateContext() => new(DbOptions);

    protected void Seed(params SearchTermDailyEntity[] rows)
    {
        using var context = CreateContext();
        context.SearchTermDaily.AddRange(rows);
        context.SaveChanges();
    }

    protected static SearchTermDailyEntity Row(Guid site, string day, string term, int searches, int zero = 0) => new()
    {
        SiteKey = site,
        Day = DateOnly.Parse(day),
        Term = term,
        SearchCount = searches,
        ZeroResultCount = zero,
    };

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal sealed class TestContextFactory : IDbContextFactory<SearchTrackerDbContext>
{
    private readonly DbContextOptions<SearchTrackerDbContext> _options;

    public TestContextFactory(DbContextOptions<SearchTrackerDbContext> options) => _options = options;

    public SearchTrackerDbContext CreateDbContext() => new(_options);
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTimeOffset now) => Now = now;

    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestOptions
{
    public static IOptions<SearchTrackerOptions> Create(Action<SearchTrackerOptions>? configure = null)
    {
        var options = new SearchTrackerOptions();
        configure?.Invoke(options);
        return new OptionsWrapper<SearchTrackerOptions>(options);
    }
}

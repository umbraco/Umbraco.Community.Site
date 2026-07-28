using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Recording;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundHitWriterServiceTests : IDisposable
{
    private readonly DbContextOptions<NotFoundTrackerDbContext> _dbOptions;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

    public NotFoundHitWriterServiceTests()
    {
        // In-memory SQLite — held open via a single connection so the schema persists.
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<NotFoundTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new NotFoundTrackerDbContext(_dbOptions);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NotFoundTrackerDbContext CreateContext() => new(_dbOptions);

    private static IOptions<NotFoundTrackerOptions> Options(NotFoundTrackerOptions? opts = null)
        => new OptionsWrapper<NotFoundTrackerOptions>(opts ?? new NotFoundTrackerOptions
        {
            WriterFlushInterval = TimeSpan.FromMilliseconds(50),
            WriterBatchSize = 100,
            ChannelCapacity = 1000,
        });

    [Fact]
    public async Task Writer_upserts_a_new_hit_row()
    {
        var options = Options();
        var channel = new NotFoundHitChannel(options);
        var factory = new TestContextFactory(_dbOptions);
        var writer = new NotFoundHitWriterService(channel, factory, options, NullLogger<NotFoundHitWriterService>.Instance);

        var evt = new NotFoundHitEvent("example.com", "/foo", null, "TestUA", DateTime.UtcNow);
        channel.Writer.TryWrite(evt).Should().BeTrue();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = writer.StartAsync(cts.Token);

        // Wait for the row to appear (writer flushes every 50ms).
        await WaitForRowCountAsync(1, cts.Token);

        await writer.StopAsync(CancellationToken.None);

        using var context = CreateContext();
        var rows = await context.NotFoundHits.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Hostname.Should().Be("example.com");
        rows[0].Path.Should().Be("/foo");
        rows[0].HitCount.Should().Be(1);
        rows[0].LastUserAgent.Should().Be("TestUA");
    }

    [Fact]
    public async Task Writer_increments_hit_count_on_repeated_hits()
    {
        var options = Options();
        var channel = new NotFoundHitChannel(options);
        var factory = new TestContextFactory(_dbOptions);
        var writer = new NotFoundHitWriterService(channel, factory, options, NullLogger<NotFoundHitWriterService>.Instance);

        for (var i = 0; i < 25; i++)
        {
            channel.Writer.TryWrite(new NotFoundHitEvent("example.com", "/foo", null, "UA", DateTime.UtcNow));
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await writer.StartAsync(cts.Token);
        await WaitForHitCountAsync("example.com", "/foo", 25, cts.Token);
        await writer.StopAsync(CancellationToken.None);

        using var context = CreateContext();
        var row = await context.NotFoundHits.SingleAsync();
        row.HitCount.Should().Be(25);
    }

    [Fact]
    public async Task Writer_records_query_string_children()
    {
        var options = Options();
        var channel = new NotFoundHitChannel(options);
        var factory = new TestContextFactory(_dbOptions);
        var writer = new NotFoundHitWriterService(channel, factory, options, NullLogger<NotFoundHitWriterService>.Instance);

        channel.Writer.TryWrite(new NotFoundHitEvent("example.com", "/foo", "?a=1", "UA", DateTime.UtcNow));
        channel.Writer.TryWrite(new NotFoundHitEvent("example.com", "/foo", "?a=1", "UA", DateTime.UtcNow));
        channel.Writer.TryWrite(new NotFoundHitEvent("example.com", "/foo", "?a=2", "UA", DateTime.UtcNow));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await writer.StartAsync(cts.Token);
        await WaitForRowCountAsync(1, cts.Token);
        await Task.Delay(150, cts.Token); // give the writer one more cycle
        await writer.StopAsync(CancellationToken.None);

        using var context = CreateContext();
        var qsRows = await context.NotFoundHitQueryStrings.ToListAsync();
        qsRows.Should().HaveCount(2);
        qsRows.Single(q => q.QueryString == "?a=1").HitCount.Should().Be(2);
        qsRows.Single(q => q.QueryString == "?a=2").HitCount.Should().Be(1);
    }

    [Fact]
    public async Task Writer_drains_remaining_events_on_shutdown()
    {
        var options = Options(new NotFoundTrackerOptions
        {
            WriterFlushInterval = TimeSpan.FromSeconds(10),  // long — would normally not flush
            WriterBatchSize = 100,
            ChannelCapacity = 1000,
        });
        var channel = new NotFoundHitChannel(options);
        var factory = new TestContextFactory(_dbOptions);
        var writer = new NotFoundHitWriterService(channel, factory, options, NullLogger<NotFoundHitWriterService>.Instance);

        channel.Writer.TryWrite(new NotFoundHitEvent("example.com", "/late", null, null, DateTime.UtcNow));

        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(50);  // event in channel, not yet flushed by timer
        await writer.StopAsync(CancellationToken.None);

        using var context = CreateContext();
        var rows = await context.NotFoundHits.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Path.Should().Be("/late");
    }

    private async Task WaitForRowCountAsync(int expected, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < deadline)
        {
            using var context = CreateContext();
            if (await context.NotFoundHits.CountAsync(ct) >= expected) return;
            await Task.Delay(25, ct);
        }
        throw new TimeoutException($"Expected {expected} row(s) within timeout.");
    }

    private async Task WaitForHitCountAsync(string host, string path, long expected, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < deadline)
        {
            using var context = CreateContext();
            var row = await context.NotFoundHits.FirstOrDefaultAsync(h => h.Hostname == host && h.Path == path, ct);
            if (row?.HitCount >= expected) return;
            await Task.Delay(25, ct);
        }
        throw new TimeoutException($"HitCount did not reach {expected} within timeout.");
    }

    private sealed class TestContextFactory : IDbContextFactory<NotFoundTrackerDbContext>
    {
        private readonly DbContextOptions<NotFoundTrackerDbContext> _options;
        public TestContextFactory(DbContextOptions<NotFoundTrackerDbContext> options) => _options = options;
        public NotFoundTrackerDbContext CreateDbContext() => new(_options);
    }
}

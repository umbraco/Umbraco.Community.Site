using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UmbracoCommunity.BlogAnnouncements.Infrastructure;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;
using UmbracoCommunity.BlogAnnouncements.Monitoring;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class AnnouncementHeartbeatMonitorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteContextFactory _factory = new();

    private static BlogAnnouncementsOptions Enabled(int staleAfterMinutes = 90, int repeatAfterHours = 6)
        => new()
        {
            Monitoring = new MonitoringOptions
            {
                Enabled = true,
                StaleAfterMinutes = staleAfterMinutes,
                RepeatAfterHours = repeatAfterHours,
            },
        };

    private (AnnouncementHeartbeatMonitor Monitor, RecordingAlerter Alerter) Create(
        BlogAnnouncementsOptions options,
        DateTimeOffset? now = null)
    {
        var alerter = new RecordingAlerter();
        var monitor = new AnnouncementHeartbeatMonitor(
            _factory,
            alerter,
            new OptionsMonitorStub<BlogAnnouncementsOptions>(options),
            new MutableTime(now ?? Now),
            NullLogger<AnnouncementHeartbeatMonitor>.Instance);
        return (monitor, alerter);
    }

    private async Task SeedRunAsync(DateTime runUtc)
    {
        await using var db = _factory.CreateDbContext();
        db.AnnouncementRuns.Add(new AnnouncementRun { RunUtc = runUtc });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LastRunOlderThanThreshold_Alerts()
    {
        // The real outage: the last cycle ran at 08:46, nothing since.
        await SeedRunAsync(Now.UtcDateTime.AddHours(-3));
        var (monitor, alerter) = Create(Enabled());

        var result = await monitor.CheckAsync();

        result.Stale.Should().BeTrue();
        result.Alerted.Should().BeTrue();
        alerter.Alerts.Should().ContainSingle();
        alerter.Alerts[0].Age.Should().Be(TimeSpan.FromHours(3));
    }

    [Fact]
    public async Task LastRunWithinThreshold_DoesNotAlert()
    {
        await SeedRunAsync(Now.UtcDateTime.AddMinutes(-20));
        var (monitor, alerter) = Create(Enabled());

        var result = await monitor.CheckAsync();

        result.Stale.Should().BeFalse();
        alerter.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task NoRunsAtAll_DoesNotAlert()
    {
        // A pipeline that has never run (fresh install) is not an outage worth waking anyone for.
        var (monitor, alerter) = Create(Enabled());

        var result = await monitor.CheckAsync();

        result.Stale.Should().BeFalse();
        result.Age.Should().BeNull();
        alerter.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Disabled_DoesNotAlertEvenWhenStale()
    {
        await SeedRunAsync(Now.UtcDateTime.AddDays(-5));
        var options = Enabled();
        options.Monitoring.Enabled = false;
        var (monitor, alerter) = Create(options);

        var result = await monitor.CheckAsync();

        result.Alerted.Should().BeFalse();
        alerter.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task StillStaleShortlyAfterAlerting_DoesNotRepeat()
    {
        await SeedRunAsync(Now.UtcDateTime.AddHours(-3));
        var alerter = new RecordingAlerter();
        var time = new MutableTime(Now);
        var monitor = CreateWith(alerter, time, Enabled());

        await monitor.CheckAsync();
        time.Advance(TimeSpan.FromMinutes(30));
        var second = await monitor.CheckAsync();

        second.Stale.Should().BeTrue();
        second.Alerted.Should().BeFalse();
        alerter.Alerts.Should().ContainSingle("a continuing outage should not alert on every check");
    }

    [Fact]
    public async Task StillStaleAfterRepeatInterval_AlertsAgain()
    {
        await SeedRunAsync(Now.UtcDateTime.AddHours(-3));
        var alerter = new RecordingAlerter();
        var time = new MutableTime(Now);
        var monitor = CreateWith(alerter, time, Enabled(repeatAfterHours: 6));

        await monitor.CheckAsync();
        time.Advance(TimeSpan.FromHours(7));
        var second = await monitor.CheckAsync();

        second.Alerted.Should().BeTrue();
        alerter.Alerts.Should().HaveCount(2);
    }

    [Fact]
    public async Task RecoversThenGoesStaleAgain_AlertsAgainImmediately()
    {
        await SeedRunAsync(Now.UtcDateTime.AddHours(-3));
        var alerter = new RecordingAlerter();
        var time = new MutableTime(Now);
        var monitor = CreateWith(alerter, time, Enabled());

        await monitor.CheckAsync();

        // The pipeline comes back: a fresh cycle lands, and the next check is healthy.
        time.Advance(TimeSpan.FromMinutes(10));
        await SeedRunAsync(time.GetUtcNow().UtcDateTime);
        var healthy = await monitor.CheckAsync();
        healthy.Stale.Should().BeFalse();

        // Then it dies again — that is a new outage, not a continuation of the old one.
        time.Advance(TimeSpan.FromHours(2));
        var second = await monitor.CheckAsync();

        second.Alerted.Should().BeTrue();
        alerter.Alerts.Should().HaveCount(2);
    }

    [Fact]
    public async Task AlertDeliveryFails_DoesNotCountAsAlerted()
    {
        // A webhook that rejected the alert must not suppress the next attempt.
        await SeedRunAsync(Now.UtcDateTime.AddHours(-3));
        var alerter = new RecordingAlerter { Result = false };
        var time = new MutableTime(Now);
        var monitor = CreateWith(alerter, time, Enabled());

        var first = await monitor.CheckAsync();
        time.Advance(TimeSpan.FromMinutes(30));
        await monitor.CheckAsync();

        first.Alerted.Should().BeFalse();
        alerter.Alerts.Should().HaveCount(2);
    }

    private AnnouncementHeartbeatMonitor CreateWith(
        RecordingAlerter alerter,
        MutableTime time,
        BlogAnnouncementsOptions options)
        => new(
            _factory,
            alerter,
            new OptionsMonitorStub<BlogAnnouncementsOptions>(options),
            time,
            NullLogger<AnnouncementHeartbeatMonitor>.Instance);

    public void Dispose() => _factory.Dispose();

    private sealed class RecordingAlerter : IHeartbeatAlerter
    {
        public List<StaleRunAlert> Alerts { get; } = [];

        public bool Result { get; set; } = true;

        public Task<bool> AlertAsync(StaleRunAlert alert, CancellationToken cancellationToken)
        {
            Alerts.Add(alert);
            return Task.FromResult(Result);
        }
    }

    private sealed class OptionsMonitorStub<T> : IOptionsMonitor<T>
    {
        public OptionsMonitorStub(T value) => CurrentValue = value;

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    /// <summary>A clock the test can advance, so repeat/recovery behaviour is exercised in one run.</summary>
    private sealed class MutableTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class SqliteContextFactory : IDbContextFactory<BlogAnnouncementsDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;

        public SqliteContextFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var ctx = CreateDbContext();
            ctx.Database.EnsureCreated();
        }

        public BlogAnnouncementsDbContext CreateDbContext()
            => new(new DbContextOptionsBuilder<BlogAnnouncementsDbContext>().UseSqlite(_connection).Options);

        public void Dispose() => _connection.Dispose();
    }
}

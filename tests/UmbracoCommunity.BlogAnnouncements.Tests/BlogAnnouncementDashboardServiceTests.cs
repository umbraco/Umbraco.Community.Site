using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UmbracoCommunity.BlogAnnouncements;
using UmbracoCommunity.BlogAnnouncements.Dashboard;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using UmbracoCommunity.BlogAnnouncements.Infrastructure;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class BlogAnnouncementDashboardServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteContextFactory _factory = new();

    private BlogAnnouncementDashboardService CreateService(
        IDiscordAnnouncer announcer,
        BlogAnnouncementsOptions? options = null)
        => new(
            _factory,
            announcer,
            new OptionsMonitorStub<BlogAnnouncementsOptions>(options ?? new BlogAnnouncementsOptions()),
            new FrozenTime(Now),
            NullLogger<BlogAnnouncementDashboardService>.Instance);

    private AnnouncedBlogPost Seed(
        AnnouncementStatus status,
        DateTime? publishedAtUtc = null,
        string title = "A Post",
        string? author = "Jane")
    {
        var post = new AnnouncedBlogPost
        {
            PlatformPostId = Guid.NewGuid(),
            Url = "https://blog.example/a",
            Title = title,
            PublishedAtUtc = publishedAtUtc ?? Now.UtcDateTime.AddDays(-1),
            Fingerprint = Guid.NewGuid().ToString(),
            FirstSeenUtc = Now.UtcDateTime,
            Status = status,
            AuthorName = author,
        };
        using var db = _factory.CreateDbContext();
        db.AnnouncedBlogPosts.Add(post);
        db.SaveChanges();
        return post;
    }

    [Fact]
    public async Task ListPosts_DefaultWindow_ExcludesPostsOlderThan30Days()
    {
        Seed(AnnouncementStatus.Announced, publishedAtUtc: Now.UtcDateTime.AddDays(-5), title: "Recent");
        Seed(AnnouncementStatus.SkippedTooOld, publishedAtUtc: Now.UtcDateTime.AddDays(-45), title: "Old");
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        var result = await service.ListPostsAsync(new PostQuery(null, null, null, null, 0, 25), default);

        result.Total.Should().Be(1);
        result.Items.Single().Title.Should().Be("Recent");
    }

    [Fact]
    public async Task ListPosts_FiltersByStatusAndSearch()
    {
        Seed(AnnouncementStatus.Announced, title: "Umbraco 18 released", author: "Jane");
        Seed(AnnouncementStatus.Pending, title: "Umbraco 18 released", author: "Bob");
        Seed(AnnouncementStatus.Announced, title: "Something else", author: "Carol");
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        var byStatus = await service.ListPostsAsync(
            new PostQuery(AnnouncementStatus.Announced, null, null, null, 0, 25), default);
        byStatus.Total.Should().Be(2);

        var bySearch = await service.ListPostsAsync(
            new PostQuery(AnnouncementStatus.Announced, "bob", null, null, 0, 25), default);
        bySearch.Total.Should().Be(0); // Bob's post is Pending, not Announced

        var searchTitle = await service.ListPostsAsync(
            new PostQuery(null, "umbraco 18", null, null, 0, 25), default);
        searchTitle.Total.Should().Be(2);
    }

    [Fact]
    public async Task Announce_Repost_Success_MarksAnnouncedAndRecordsAttempt()
    {
        var post = Seed(AnnouncementStatus.Announced);
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var service = CreateService(announcer, new BlogAnnouncementsOptions { DryRun = false });

        var result = await service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.Repost, default);

        result.Outcome.Should().Be(ManualAnnounceOutcome.Delivered);
        result.Status.Should().Be(AnnouncementStatus.Announced);
        result.AnnouncedUtc.Should().Be(Now.UtcDateTime);
        announcer.Calls.Should().Be(1);

        await using var db = _factory.CreateDbContext();
        var attempt = await db.AnnouncementAttempts.SingleAsync();
        attempt.Trigger.Should().Be(AnnouncementTrigger.Repost);
        attempt.Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Announce_PostNow_OnAlreadyAnnounced_IsRejected()
    {
        var post = Seed(AnnouncementStatus.Announced);
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var service = CreateService(announcer, new BlogAnnouncementsOptions { DryRun = false });

        var result = await service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.PostNow, default);

        result.Outcome.Should().Be(ManualAnnounceOutcome.InvalidStatusForPostNow);
        announcer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Announce_PostNow_OnSkippedTooOld_Delivers()
    {
        var post = Seed(AnnouncementStatus.SkippedTooOld);
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var service = CreateService(announcer, new BlogAnnouncementsOptions { DryRun = false });

        var result = await service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.PostNow, default);

        result.Outcome.Should().Be(ManualAnnounceOutcome.Delivered);
        result.Status.Should().Be(AnnouncementStatus.Announced);
    }

    [Fact]
    public async Task Announce_DryRun_LeavesStatusAndRecordsDryRunAttempt()
    {
        var post = Seed(AnnouncementStatus.Pending);
        var announcer = new RecordingAnnouncer(DeliveryResult.Dry);
        var service = CreateService(announcer, new BlogAnnouncementsOptions { DryRun = true });

        var result = await service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.PostNow, default);

        result.Outcome.Should().Be(ManualAnnounceOutcome.Delivered);
        result.Status.Should().Be(AnnouncementStatus.Pending);
        result.AnnouncedUtc.Should().BeNull();

        await using var db = _factory.CreateDbContext();
        (await db.AnnouncementAttempts.SingleAsync()).Outcome.Should().Be("DryRun");
    }

    [Fact]
    public async Task Announce_UnknownPost_ReturnsNotFound()
    {
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        var result = await service.AnnounceAsync(Guid.NewGuid(), AnnouncementTrigger.Repost, default);

        result.Outcome.Should().Be(ManualAnnounceOutcome.PostNotFound);
    }

    [Fact]
    public async Task Announce_InvalidTrigger_IsRejected()
    {
        var post = Seed(AnnouncementStatus.Announced);
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        var result = await service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.Auto, default);

        result.Outcome.Should().Be(ManualAnnounceOutcome.InvalidTrigger);
    }

    [Fact]
    public async Task Announce_ConcurrentDeliveryForSamePost_IsGuarded()
    {
        var post = Seed(AnnouncementStatus.Announced);
        var gate = new GatedAnnouncer();
        var service = CreateService(gate, new BlogAnnouncementsOptions { DryRun = false });

        // First call enters the announcer and blocks on the gate.
        var first = service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.Repost, default);
        await gate.Entered.Task; // ensure the first call holds the in-flight slot

        // Second call for the same post while the first is in flight.
        var second = await service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.Repost, default);
        second.Outcome.Should().Be(ManualAnnounceOutcome.InFlight);

        gate.Release.SetResult(DeliveryResult.Ok(204));
        (await first).Outcome.Should().Be(ManualAnnounceOutcome.Delivered);
    }

    [Fact]
    public async Task Reset_AnnouncedPost_BecomesPendingWithHistoryKeptAndResetAttemptAppended()
    {
        var post = Seed(AnnouncementStatus.Announced);
        using (var db = _factory.CreateDbContext())
        {
            db.AnnouncementAttempts.Add(new AnnouncementAttempt
            {
                PlatformPostId = post.PlatformPostId,
                AttemptedUtc = Now.UtcDateTime.AddHours(-1),
                Outcome = "Success",
                HttpStatus = 204,
                Trigger = AnnouncementTrigger.Auto,
            });
            db.SaveChanges();
        }
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        var outcome = await service.ResetAsync(post.PlatformPostId, default);

        outcome.Should().Be(ResetOutcome.Reset);
        await using var verify = _factory.CreateDbContext();
        var row = await verify.AnnouncedBlogPosts.SingleAsync();
        row.Status.Should().Be(AnnouncementStatus.Pending);
        row.AnnouncedUtc.Should().BeNull();

        var attempts = await verify.AnnouncementAttempts.OrderBy(a => a.AttemptedUtc).ToListAsync();
        attempts.Should().HaveCount(2); // original delivery history kept + reset row appended
        attempts[1].Outcome.Should().Be("Reset");
        attempts[1].Trigger.Should().Be(AnnouncementTrigger.Reset);
        attempts[1].HttpStatus.Should().BeNull();
        attempts[1].AttemptedUtc.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public async Task Reset_FailedPost_IsAllowed()
    {
        var post = Seed(AnnouncementStatus.Failed);
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        (await service.ResetAsync(post.PlatformPostId, default)).Should().Be(ResetOutcome.Reset);

        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.SingleAsync()).Status.Should().Be(AnnouncementStatus.Pending);
    }

    [Theory]
    [InlineData(AnnouncementStatus.Pending)]
    [InlineData(AnnouncementStatus.SkippedTooOld)]
    [InlineData(AnnouncementStatus.Suppressed)]
    public async Task Reset_NonAnnouncedStatuses_AreRejected(AnnouncementStatus status)
    {
        var post = Seed(status);
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        (await service.ResetAsync(post.PlatformPostId, default)).Should().Be(ResetOutcome.InvalidStatus);

        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.SingleAsync()).Status.Should().Be(status);
        (await db.AnnouncementAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reset_UnknownPost_ReturnsNotFound()
    {
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));
        (await service.ResetAsync(Guid.NewGuid(), default)).Should().Be(ResetOutcome.PostNotFound);
    }

    [Fact]
    public async Task Reset_WhileDeliveryInFlight_IsGuarded()
    {
        var post = Seed(AnnouncementStatus.Announced);
        var gate = new GatedAnnouncer();
        var service = CreateService(gate, new BlogAnnouncementsOptions { DryRun = false });

        // Hold a manual delivery in flight for the same post.
        var delivery = service.AnnounceAsync(post.PlatformPostId, AnnouncementTrigger.Repost, default);
        await gate.Entered.Task;

        (await service.ResetAsync(post.PlatformPostId, default)).Should().Be(ResetOutcome.InFlight);

        gate.Release.SetResult(DeliveryResult.Ok(204));
        (await delivery).Outcome.Should().Be(ManualAnnounceOutcome.Delivered);
    }

    [Fact]
    public void GetSettings_ReflectsOptionsAndWebhookConfigured()
    {
        var options = new BlogAnnouncementsOptions
        {
            RecencyWindowDays = 14,
            MaxAnnouncementsPerCycle = 3,
            DryRun = false,
            Discord = new DiscordAnnouncementOptions { WebhookUrl = "https://discord.example/webhook" },
        };
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)), options);

        var settings = service.GetSettings();

        settings.RecencyWindowDays.Should().Be(14);
        settings.MaxAnnouncementsPerCycle.Should().Be(3);
        settings.DryRun.Should().BeFalse();
        settings.WebhookConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Timestamps_AreStampedUtc_AfterDatabaseRoundtrip()
    {
        // EF materializes DateTimeKind.Unspecified from SQLite; the DTO layer must stamp UTC so
        // System.Text.Json emits a trailing "Z" and browsers don't parse the value as local time.
        var post = Seed(AnnouncementStatus.Announced);
        using (var db = _factory.CreateDbContext())
        {
            db.AnnouncementAttempts.Add(new AnnouncementAttempt
            {
                PlatformPostId = post.PlatformPostId,
                AttemptedUtc = Now.UtcDateTime,
                Outcome = "Success",
                Trigger = AnnouncementTrigger.Auto,
            });
            db.AnnouncementRuns.Add(new AnnouncementRun { RunUtc = Now.UtcDateTime, Fetched = 1 });
            db.SaveChanges();
        }
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)));

        var listItem = (await service.ListPostsAsync(new PostQuery(null, null, null, null, 0, 25), default)).Items.Single();
        listItem.PublishedAtUtc.Kind.Should().Be(DateTimeKind.Utc);

        var detail = (await service.GetPostAsync(post.PlatformPostId, default))!;
        detail.PublishedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        detail.FirstSeenUtc.Kind.Should().Be(DateTimeKind.Utc);
        detail.Attempts.Single().AttemptedUtc.Kind.Should().Be(DateTimeKind.Utc);

        var run = (await service.ListRunsAsync(0, 25, default)).Items.Single();
        run.RunUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void GetSettings_EmptyWebhook_ReportsNotConfigured()
    {
        var service = CreateService(new RecordingAnnouncer(DeliveryResult.Ok(204)), new BlogAnnouncementsOptions());
        service.GetSettings().WebhookConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task SendTestMessage_PassesThroughAnnouncerResult()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var service = CreateService(announcer, new BlogAnnouncementsOptions { DryRun = false });

        var result = await service.SendTestMessageAsync(default);

        result.Success.Should().BeTrue();
        announcer.Calls.Should().Be(1);
        announcer.Payloads.Single().Title.Should().Contain("Test message");
    }

    public void Dispose() => _factory.Dispose();

    // --- test doubles ---

    private sealed class RecordingAnnouncer : IDiscordAnnouncer
    {
        private readonly DeliveryResult _result;
        public int Calls { get; private set; }
        public List<AnnouncementPayload> Payloads { get; } = new();

        public RecordingAnnouncer(DeliveryResult result) => _result = result;

        public Task<DeliveryResult> AnnounceAsync(AnnouncementPayload payload, CancellationToken cancellationToken)
        {
            Calls++;
            Payloads.Add(payload);
            return Task.FromResult(_result);
        }
    }

    private sealed class GatedAnnouncer : IDiscordAnnouncer
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<DeliveryResult> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<DeliveryResult> AnnounceAsync(AnnouncementPayload payload, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            return await Release.Task;
        }
    }

    private sealed class OptionsMonitorStub<T> : IOptionsMonitor<T>
    {
        public OptionsMonitorStub(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FrozenTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FrozenTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
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
        {
            var options = new DbContextOptionsBuilder<BlogAnnouncementsDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new BlogAnnouncementsDbContext(options);
        }

        public void Dispose() => _connection.Dispose();
    }
}

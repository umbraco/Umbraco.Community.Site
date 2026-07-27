using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UmbracoCommunity.BlogAnnouncements;
using UmbracoCommunity.BlogAnnouncements.Delivery;
using UmbracoCommunity.BlogAnnouncements.Detection;
using UmbracoCommunity.BlogAnnouncements.Infrastructure;
using UmbracoCommunity.BlogAnnouncements.Models.Entities;
using Xunit;

namespace UmbracoCommunity.BlogAnnouncements.Tests;

public class BlogAnnouncementDetectorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteContextFactory _factory = new();

    private BlogAnnouncementDetector CreateDetector(RecordingAnnouncer announcer, BlogAnnouncementsOptions options)
        => new(
            _factory,
            announcer,
            new OptionsMonitorStub<BlogAnnouncementsOptions>(options),
            new FrozenTime(Now),
            NullLogger<BlogAnnouncementDetector>.Instance);

    private static AnnouncementCandidatePost[] Data(params AnnouncementCandidatePost[] posts)
        => posts;

    private static AnnouncementCandidatePost Post(
        string id,
        DateTimeOffset publishedAt,
        string title = "A Post",
        string? author = "Jane",
        string url = "https://blog.example/a",
        string? avatar = "https://cdn/a.png")
        => new(id, title, url, "excerpt", "https://cdn/c.png", publishedAt, author, avatar, "https://profile/jane");

    [Fact]
    public async Task NewWithinWindow_DryRun_LeavesPendingAndRecordsDryRunAttempt()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Dry);
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = true });

        await detector.DetectAndAnnounceAsync(Data(Post(Guid.NewGuid().ToString(), Now.AddDays(-1))));

        await using var db = _factory.CreateDbContext();
        var row = await db.AnnouncedBlogPosts.SingleAsync();
        row.Status.Should().Be(AnnouncementStatus.Pending);
        row.AnnouncedUtc.Should().BeNull();
        (await db.AnnouncementAttempts.SingleAsync()).Outcome.Should().Be("DryRun");
        var run = await db.AnnouncementRuns.SingleAsync();
        run.DryRun.Should().BeTrue();
        run.New.Should().Be(1);
        run.Announced.Should().Be(0);
        announcer.Calls.Should().Be(1);
    }

    [Fact]
    public async Task NewWithinWindow_LiveSuccess_MarksAnnounced()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false });

        await detector.DetectAndAnnounceAsync(Data(Post(Guid.NewGuid().ToString(), Now.AddDays(-1))));

        await using var db = _factory.CreateDbContext();
        var row = await db.AnnouncedBlogPosts.SingleAsync();
        row.Status.Should().Be(AnnouncementStatus.Announced);
        row.AnnouncedUtc.Should().Be(Now.UtcDateTime);
        (await db.AnnouncementAttempts.SingleAsync()).Outcome.Should().Be("Success");
        (await db.AnnouncementRuns.SingleAsync()).Announced.Should().Be(1);
    }

    [Fact]
    public async Task OlderThanWindow_RecordedSkippedTooOld_NotDelivered()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false, RecencyWindowDays = 7 });

        await detector.DetectAndAnnounceAsync(Data(Post(Guid.NewGuid().ToString(), Now.AddDays(-30))));

        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.SingleAsync()).Status.Should().Be(AnnouncementStatus.SkippedTooOld);
        announcer.Calls.Should().Be(0);
        (await db.AnnouncementRuns.SingleAsync()).Skipped.Should().Be(1);
    }

    [Fact]
    public async Task ExceedingCap_LeavesRemainderPending()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false, MaxAnnouncementsPerCycle = 2 });

        var posts = Enumerable.Range(0, 5)
            .Select(i => Post(Guid.NewGuid().ToString(), Now.AddDays(-1).AddMinutes(-i), title: $"Post {i}"))
            .ToArray();
        await detector.DetectAndAnnounceAsync(Data(posts));

        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.CountAsync(p => p.Status == AnnouncementStatus.Announced)).Should().Be(2);
        (await db.AnnouncedBlogPosts.CountAsync(p => p.Status == AnnouncementStatus.Pending)).Should().Be(3);
    }

    [Fact]
    public async Task StaleAvatarOnPendingRow_IsRefreshedBeforeDelivery()
    {
        var id = Guid.NewGuid().ToString();

        // Cycle 1 (dry-run) records the post as Pending with the broken avatar the platform served then.
        var dryRun = CreateDetector(new RecordingAnnouncer(DeliveryResult.Dry), new BlogAnnouncementsOptions { DryRun = true });
        await dryRun.DetectAndAnnounceAsync(Data(Post(id, Now.AddDays(-1), avatar: "https://linkedin.example/broken.jpg")));

        // Cycle 2 (live): the platform has corrected the avatar; delivery must carry the fresh one.
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var live = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false });
        await live.DetectAndAnnounceAsync(Data(Post(id, Now.AddDays(-1), avatar: "https://github.example/fixed.png")));

        announcer.Payloads.Single().AvatarUrl.Should().Be("https://github.example/fixed.png");
        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.SingleAsync()).AuthorAvatarUrl.Should().Be("https://github.example/fixed.png");
    }

    [Fact]
    public async Task UnchangedTrackedPost_ProducesNoMetadataUpdate()
    {
        var id = Guid.NewGuid().ToString();
        var post = Post(id, Now.AddDays(-1));

        var detector1 = CreateDetector(new RecordingAnnouncer(DeliveryResult.Ok(204)), new BlogAnnouncementsOptions { DryRun = false });
        await detector1.DetectAndAnnounceAsync(Data(post));

        AnnouncedBlogPost before;
        await using (var db = _factory.CreateDbContext())
        {
            before = await db.AnnouncedBlogPosts.AsNoTracking().SingleAsync();
        }

        // Same post again next cycle — nothing about the row may change.
        var detector2 = CreateDetector(new RecordingAnnouncer(DeliveryResult.Ok(204)), new BlogAnnouncementsOptions { DryRun = false });
        await detector2.DetectAndAnnounceAsync(Data(post));

        await using var verify = _factory.CreateDbContext();
        var after = await verify.AnnouncedBlogPosts.AsNoTracking().SingleAsync();
        after.Should().BeEquivalentTo(before, o => o.Excluding(p => p.Attempts));
        (await verify.AnnouncementAttempts.CountAsync()).Should().Be(1); // only the original delivery
    }

    [Fact]
    public async Task ChangedTitle_RecomputesFingerprint_WithoutTouchingStatusOrHistory()
    {
        var id = Guid.NewGuid().ToString();
        var publishedAt = Now.AddDays(-1);

        var detector1 = CreateDetector(new RecordingAnnouncer(DeliveryResult.Ok(204)), new BlogAnnouncementsOptions { DryRun = false });
        await detector1.DetectAndAnnounceAsync(Data(Post(id, publishedAt, title: "Old Title")));

        var detector2 = CreateDetector(new RecordingAnnouncer(DeliveryResult.Ok(204)), new BlogAnnouncementsOptions { DryRun = false });
        await detector2.DetectAndAnnounceAsync(Data(Post(id, publishedAt, title: "Corrected Title")));

        await using var db = _factory.CreateDbContext();
        var row = await db.AnnouncedBlogPosts.SingleAsync();
        row.Title.Should().Be("Corrected Title");
        row.Fingerprint.Should().Be(AnnouncementFingerprint.Compute("Jane", "Corrected Title", publishedAt));
        row.Status.Should().Be(AnnouncementStatus.Announced); // untouched by the metadata refresh
        row.AnnouncedUtc.Should().Be(Now.UtcDateTime);
        (await db.AnnouncementAttempts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delivery_IsOldestFirst_RegardlessOfInputOrder()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false });

        // Fed newest-first (the typical feed order) — delivery must still be chronological.
        var newest = Post(Guid.NewGuid().ToString(), Now.AddHours(-1), title: "Newest");
        var middle = Post(Guid.NewGuid().ToString(), Now.AddHours(-12), title: "Middle");
        var oldest = Post(Guid.NewGuid().ToString(), Now.AddDays(-2), title: "Oldest");
        await detector.DetectAndAnnounceAsync(Data(newest, middle, oldest));

        announcer.Payloads.Select(p => p.Title).Should().Equal("Oldest", "Middle", "Newest");
        announcer.Payloads.Select(p => p.PublishedAt).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task SamePlatformPostId_NotReinserted_AndNotReannounced()
    {
        var id = Guid.NewGuid().ToString();
        var detector1 = CreateDetector(new RecordingAnnouncer(DeliveryResult.Ok(204)), new BlogAnnouncementsOptions { DryRun = false });
        await detector1.DetectAndAnnounceAsync(Data(Post(id, Now.AddDays(-1))));

        var announcer2 = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector2 = CreateDetector(announcer2, new BlogAnnouncementsOptions { DryRun = false });
        await detector2.DetectAndAnnounceAsync(Data(Post(id, Now.AddDays(-1))));

        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.CountAsync()).Should().Be(1);
        announcer2.Calls.Should().Be(0); // already Announced — nothing left to deliver
    }

    [Fact]
    public async Task DuplicateByFingerprint_DifferentId_IsIgnored()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false });

        var day = Now.AddDays(-1);
        // Same author + title + day, different platform post id and URL (the two-domain case).
        var a = Post(Guid.NewGuid().ToString(), day, title: "Same", url: "https://custom.example/p");
        var b = Post(Guid.NewGuid().ToString(), day, title: "Same", url: "https://app.azurewebsites.net/p");
        await detector.DetectAndAnnounceAsync(Data(a, b));

        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FailedDelivery_MarksFailed_ThenRetriesToSuccess()
    {
        var id = Guid.NewGuid().ToString();
        var failing = new RecordingAnnouncer(DeliveryResult.Fail(500));
        var detector1 = CreateDetector(failing, new BlogAnnouncementsOptions { DryRun = false });
        await detector1.DetectAndAnnounceAsync(Data(Post(id, Now.AddDays(-1))));

        await using (var db = _factory.CreateDbContext())
        {
            (await db.AnnouncedBlogPosts.SingleAsync()).Status.Should().Be(AnnouncementStatus.Failed);
        }

        var succeeding = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector2 = CreateDetector(succeeding, new BlogAnnouncementsOptions { DryRun = false });
        // Same post shows up again next cycle; Failed rows are retried even if already tracked.
        await detector2.DetectAndAnnounceAsync(Data(Post(id, Now.AddDays(-1))));

        await using var db2 = _factory.CreateDbContext();
        (await db2.AnnouncedBlogPosts.SingleAsync()).Status.Should().Be(AnnouncementStatus.Announced);
        succeeding.Calls.Should().Be(1);
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

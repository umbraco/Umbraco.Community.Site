using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    private BlogAnnouncementDetector CreateDetector(IDiscordAnnouncer announcer, BlogAnnouncementsOptions options)
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

    /// <summary>
    /// The double-announce regression: two cycles overlapping (a second app instance, or a
    /// schedule that fires twice per tick) both read the same Pending row. The second one must
    /// find it claimed and skip it, rather than sending the same post to Discord again.
    /// </summary>
    [Fact]
    public async Task ConcurrentCycle_ArrivingMidDelivery_DoesNotAnnounceTheSamePostTwice()
    {
        var id = Guid.NewGuid().ToString();
        var options = new BlogAnnouncementsOptions { DryRun = false };

        // The interloper runs a full second cycle from inside the first cycle's webhook call —
        // exactly the window where the row used to still look deliverable.
        var interloper = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var interloperDetector = CreateDetector(interloper, options);

        var firstCycle = new ReentrantAnnouncer(
            DeliveryResult.Ok(204),
            () => interloperDetector.AnnounceQueuedAsync(0, 0, 0));
        var detector = CreateDetector(firstCycle, options);

        await detector.DetectAndAnnounceAsync(Data(Post(id, Now.AddDays(-1))));

        firstCycle.Calls.Should().Be(1);
        interloper.Calls.Should().Be(0, "the concurrent cycle should have found the post claimed");

        await using var db = _factory.CreateDbContext();
        var row = await db.AnnouncedBlogPosts.SingleAsync();
        row.Status.Should().Be(AnnouncementStatus.Announced);
        row.ClaimedUtc.Should().BeNull("a settled row holds no claim");
        (await db.AnnouncementAttempts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DryRun_ReleasesTheClaim_LeavingThePostDeliverableNextCycle()
    {
        var announcer = new RecordingAnnouncer(DeliveryResult.Dry);
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = true });

        await detector.DetectAndAnnounceAsync(Data(Post(Guid.NewGuid().ToString(), Now.AddDays(-1))));

        await using var db = _factory.CreateDbContext();
        var row = await db.AnnouncedBlogPosts.SingleAsync();
        row.Status.Should().Be(AnnouncementStatus.Pending);
        row.ClaimedUtc.Should().BeNull();
    }

    /// <summary>
    /// A process that dies between claiming and settling would otherwise leave the row stuck in
    /// flight forever; the next cycle reverts the abandoned claim and retries it.
    /// </summary>
    [Fact]
    public async Task StaleClaim_IsRevertedAndRetried()
    {
        var id = Guid.NewGuid();
        await using (var seed = _factory.CreateDbContext())
        {
            seed.AnnouncedBlogPosts.Add(new AnnouncedBlogPost
            {
                PlatformPostId = id,
                Url = "https://blog.example/a",
                Title = "Abandoned mid-send",
                PublishedAtUtc = Now.AddDays(-1).UtcDateTime,
                Fingerprint = "jane|abandoned mid-send|2026-06-14",
                FirstSeenUtc = Now.AddDays(-1).UtcDateTime,
                Status = AnnouncementStatus.Claimed,
                ClaimedUtc = Now.UtcDateTime - TimeSpan.FromHours(1),
                AuthorName = "Jane",
            });
            await seed.SaveChangesAsync();
        }

        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false });

        await detector.AnnounceQueuedAsync(0, 0, 0);

        announcer.Calls.Should().Be(1);
        await using var db = _factory.CreateDbContext();
        var row = await db.AnnouncedBlogPosts.SingleAsync();
        row.Status.Should().Be(AnnouncementStatus.Announced);
        row.ClaimedUtc.Should().BeNull();
    }

    [Fact]
    public async Task FreshClaim_IsLeftAlone_NotStolenByTheNextCycle()
    {
        await using (var seed = _factory.CreateDbContext())
        {
            seed.AnnouncedBlogPosts.Add(new AnnouncedBlogPost
            {
                PlatformPostId = Guid.NewGuid(),
                Url = "https://blog.example/a",
                Title = "In flight elsewhere",
                PublishedAtUtc = Now.AddDays(-1).UtcDateTime,
                Fingerprint = "jane|in flight elsewhere|2026-06-14",
                FirstSeenUtc = Now.AddDays(-1).UtcDateTime,
                Status = AnnouncementStatus.Claimed,
                ClaimedUtc = Now.UtcDateTime - TimeSpan.FromSeconds(5),
                AuthorName = "Jane",
            });
            await seed.SaveChangesAsync();
        }

        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false });

        await detector.AnnounceQueuedAsync(0, 0, 0);

        announcer.Calls.Should().Be(0);
        await using var db = _factory.CreateDbContext();
        (await db.AnnouncedBlogPosts.SingleAsync()).Status.Should().Be(AnnouncementStatus.Claimed);
    }

    /// <summary>
    /// The ingest counterpart of the delivery race: a concurrent cycle inserts the same brand-new
    /// post between our read of the known ids and our write, so the primary key rejects ours. The
    /// cycle must drop that row and carry on rather than failing.
    /// </summary>
    [Fact]
    public async Task ConcurrentCycle_IngestingTheSamePostFirst_DoesNotFailTheCycle()
    {
        var id = Guid.NewGuid();
        var interceptor = new RacingInsertInterceptor(async () =>
        {
            await using var other = _factory.CreateRawDbContext();
            other.AnnouncedBlogPosts.Add(new AnnouncedBlogPost
            {
                PlatformPostId = id,
                Url = "https://blog.example/a",
                Title = "A Post",
                PublishedAtUtc = Now.AddDays(-1).UtcDateTime,
                Fingerprint = "jane|a post|2026-06-14",
                FirstSeenUtc = Now.UtcDateTime,
                Status = AnnouncementStatus.Pending,
                AuthorName = "Jane",
            });
            await other.SaveChangesAsync();
        });
        _factory.Interceptor = interceptor;

        var announcer = new RecordingAnnouncer(DeliveryResult.Ok(204));
        var detector = CreateDetector(announcer, new BlogAnnouncementsOptions { DryRun = false });

        await detector.DetectAndAnnounceAsync(Data(Post(id.ToString(), Now.AddDays(-1))));

        interceptor.Fired.Should().BeTrue("the test must actually have injected the racing insert");

        await using var db = _factory.CreateRawDbContext();
        (await db.AnnouncedBlogPosts.CountAsync()).Should().Be(1, "the duplicate insert must not have landed");
        // The other cycle's row is still delivered — it was Pending and this cycle claimed it.
        (await db.AnnouncedBlogPosts.SingleAsync()).Status.Should().Be(AnnouncementStatus.Announced);
        (await db.AnnouncementRuns.SingleAsync()).New.Should().Be(0, "the row was the other cycle's to count");
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

    /// <summary>
    /// Announcer that runs <paramref name="onFirstCall"/> — a whole second announcement cycle —
    /// from inside its first delivery, simulating an overlapping cycle arriving while this one is
    /// mid-send. Sequential, so the shared in-memory SQLite connection stays safe.
    /// </summary>
    private sealed class ReentrantAnnouncer : IDiscordAnnouncer
    {
        private readonly DeliveryResult _result;
        private readonly Func<Task> _onFirstCall;
        public int Calls { get; private set; }

        public ReentrantAnnouncer(DeliveryResult result, Func<Task> onFirstCall)
        {
            _result = result;
            _onFirstCall = onFirstCall;
        }

        public async Task<DeliveryResult> AnnounceAsync(AnnouncementPayload payload, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1)
            {
                await _onFirstCall();
            }

            return _result;
        }
    }

    /// <summary>
    /// Runs <paramref name="inject"/> once, immediately before the first SaveChanges of the context
    /// it is attached to — the exact window in which a concurrent cycle's insert lands.
    /// </summary>
    private sealed class RacingInsertInterceptor : SaveChangesInterceptor
    {
        private readonly Func<Task> _inject;
        public bool Fired { get; private set; }

        public RacingInsertInterceptor(Func<Task> inject) => _inject = inject;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Fired)
            {
                Fired = true;
                await _inject();
            }

            return result;
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

        /// <summary>Interceptor applied to contexts created from here; set by tests that need to
        /// act between a cycle's read and its write.</summary>
        public IInterceptor? Interceptor { get; set; }

        public BlogAnnouncementsDbContext CreateDbContext()
        {
            var builder = new DbContextOptionsBuilder<BlogAnnouncementsDbContext>()
                .UseSqlite(_connection);
            if (Interceptor is not null)
            {
                builder.AddInterceptors(Interceptor);
            }

            return new BlogAnnouncementsDbContext(builder.Options);
        }

        /// <summary>A context without the interceptor — for a test's own seeding and assertions.</summary>
        public BlogAnnouncementsDbContext CreateRawDbContext()
            => new(new DbContextOptionsBuilder<BlogAnnouncementsDbContext>().UseSqlite(_connection).Options);

        public void Dispose() => _connection.Dispose();
    }
}

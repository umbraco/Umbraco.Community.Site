using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

public class CommunityBlogsServiceTests : IDisposable
{
    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), "cb-tests-" + Guid.NewGuid().ToString("N"));

    private CommunityBlogsService CreateService(
        SphereApiClient client,
        CommunityBlogsOptions options,
        ICommunityBlogsIndexer? indexer = null)
    {
        Directory.CreateDirectory(_tempRoot);
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempRoot);

        var aggregator = new CommunityBlogsAggregator(
            client,
            new TestOptionsMonitor<CommunityBlogsOptions>(options),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-06-15T10:00:00Z")),
            NullLogger<CommunityBlogsAggregator>.Instance);

        return new CommunityBlogsService(
            aggregator,
            CreateDownloader(),
            new MemoryCache(new MemoryCacheOptions()),
            new TestOptionsMonitor<CommunityBlogsOptions>(options),
            env.Object,
            indexer ?? Mock.Of<ICommunityBlogsIndexer>(),
            NullLogger<CommunityBlogsService>.Instance);
    }

    private CommunityBlogsImageDownloader CreateDownloader()
    {
        // Posts used in these tests have null image URLs, so no HTTP/file work happens here.
        var env = new Moq.Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        env.SetupGet(e => e.WebRootPath).Returns(_tempRoot);
        var http = new HttpClient(StubHandler.Throws());
        return new CommunityBlogsImageDownloader(
            new CommunityBlogsImageHttpClient(http), env.Object,
            NullLogger<CommunityBlogsImageDownloader>.Instance);
    }

    private static SphereApiClient ClientReturning(string json)
    {
        var http = new HttpClient(StubHandler.Json(json)) { BaseAddress = new Uri("https://test.local/api/v1/") };
        return new SphereApiClient(new SphereHttpClient(http), new TestOptionsMonitor<CommunityBlogsOptions>(new()));
    }

    // First request returns the given JSON; every later request throws (network error).
    // Lets a single service instance exercise the success-then-failure refresh sequence.
    private static SphereApiClient ClientSucceedingThenFailing(string json)
    {
        var firstCall = true;
        var handler = new StubHandler(_ =>
        {
            if (firstCall)
            {
                firstCall = false;
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                };
            }

            throw new HttpRequestException("simulated network error");
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local/api/v1/") };
        return new SphereApiClient(new SphereHttpClient(http), new TestOptionsMonitor<CommunityBlogsOptions>(new()));
    }

    private static string FivePosts() => """
    {
      "data": [
        { "id": "1", "type": "blog_post", "title": "P1", "url": "https://x/1", "content": null, "coverImageUrl": null, "publishedAt": "2026-06-05T00:00:00Z", "author": null },
        { "id": "2", "type": "blog_post", "title": "P2", "url": "https://x/2", "content": null, "coverImageUrl": null, "publishedAt": "2026-06-04T00:00:00Z", "author": null },
        { "id": "3", "type": "blog_post", "title": "P3", "url": "https://x/3", "content": null, "coverImageUrl": null, "publishedAt": "2026-06-03T00:00:00Z", "author": null },
        { "id": "4", "type": "blog_post", "title": "P4", "url": "https://x/4", "content": null, "coverImageUrl": null, "publishedAt": "2026-06-02T00:00:00Z", "author": null },
        { "id": "5", "type": "blog_post", "title": "P5", "url": "https://x/5", "content": null, "coverImageUrl": null, "publishedAt": "2026-06-01T00:00:00Z", "author": null }
      ],
      "pagination": { "nextCursor": null, "hasMore": false }
    }
    """;

    [Fact]
    public async Task Refresh_then_GetData_returns_posts()
    {
        var service = CreateService(ClientReturning(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        await service.RefreshAsync();

        service.GetData().Posts.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetPage_slices_and_reports_totals()
    {
        var service = CreateService(ClientReturning(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" });
        await service.RefreshAsync();

        var page2 = service.GetPage(2, 2);

        page2.Page.Should().Be(2);
        page2.PageSize.Should().Be(2);
        page2.TotalItems.Should().Be(5);
        page2.TotalPages.Should().Be(3);
        page2.Items.Select(p => p.Id).Should().Equal("3", "4");
    }

    [Fact]
    public async Task GetPage_clamps_out_of_range_page()
    {
        var service = CreateService(ClientReturning(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" });
        await service.RefreshAsync();

        service.GetPage(99, 2).Page.Should().Be(3);
        service.GetPage(0, 2).Page.Should().Be(1);
    }

    [Fact]
    public void GetData_returns_empty_when_nothing_cached_or_on_disk()
    {
        var service = CreateService(ClientReturning(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        service.GetData().Should().BeSameAs(CommunityBlogsData.Empty);
    }

    [Fact]
    public async Task Refresh_keeps_existing_data_when_fetch_fails()
    {
        var service = CreateService(ClientReturning(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" });
        await service.RefreshAsync(); // 5 posts now cached + on disk

        // Build a fresh service over the SAME temp root with a failing client (so it reads disk).
        var failing = new HttpClient(StubHandler.Throws()) { BaseAddress = new Uri("https://test.local/api/v1/") };
        var failingClient = new SphereApiClient(new SphereHttpClient(failing), new TestOptionsMonitor<CommunityBlogsOptions>(new()));
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempRoot);
        var aggregator = new CommunityBlogsAggregator(failingClient,
            new TestOptionsMonitor<CommunityBlogsOptions>(new CommunityBlogsOptions { ApiKey = "psk_test" }),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-06-15T10:00:00Z")),
            NullLogger<CommunityBlogsAggregator>.Instance);
        var service2 = new CommunityBlogsService(aggregator, CreateDownloader(), new MemoryCache(new MemoryCacheOptions()),
            new TestOptionsMonitor<CommunityBlogsOptions>(new CommunityBlogsOptions { ApiKey = "psk_test" }),
            env.Object, Mock.Of<ICommunityBlogsIndexer>(), NullLogger<CommunityBlogsService>.Instance);

        await service2.RefreshAsync(); // fails -> must not wipe disk

        service2.GetData().Posts.Should().HaveCount(5); // read back from disk
    }

    [Fact]
    public void GetPage_on_empty_data_returns_empty_first_page()
    {
        // No RefreshAsync has run, so GetData() is empty.
        var service = CreateService(ClientReturning(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        var page = service.GetPage(1, 12);

        page.Items.Should().BeEmpty();
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(12);
        page.TotalItems.Should().Be(0);
        page.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAsync_WhenDataAggregated_RebuildsSearchIndex()
    {
        var indexer = new Mock<ICommunityBlogsIndexer>();
        var service = CreateService(ClientReturning(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" }, indexer.Object);

        await service.RefreshAsync();

        indexer.Verify(i => i.Rebuild(It.Is<CommunityBlogsData>(d => d.Posts.Count > 0)), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_WhenNoData_AndNoCache_DoesNotRebuildSearchIndex()
    {
        var indexer = new Mock<ICommunityBlogsIndexer>();
        // A failing client makes the aggregator return null, so RefreshAsync no-ops.
        // With no prior cache, GetData() is empty (Posts.Count == 0) and Rebuild is NOT called.
        var failing = new HttpClient(StubHandler.Throws()) { BaseAddress = new Uri("https://test.local/api/v1/") };
        var failingClient = new SphereApiClient(new SphereHttpClient(failing), new TestOptionsMonitor<CommunityBlogsOptions>(new()));
        var service = CreateService(failingClient,
            new CommunityBlogsOptions { ApiKey = "psk_test" }, indexer.Object);

        await service.RefreshAsync();

        indexer.Verify(i => i.Rebuild(It.IsAny<CommunityBlogsData>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WhenNoData_ButCacheHasPosts_RebuildsIndexFromCache()
    {
        var indexer = new Mock<ICommunityBlogsIndexer>();
        // One service instance: first refresh succeeds (populates cache + index),
        // second refresh fails so the aggregator returns null but the cache still has posts.
        var service = CreateService(ClientSucceedingThenFailing(FivePosts()),
            new CommunityBlogsOptions { ApiKey = "psk_test" }, indexer.Object);

        await service.RefreshAsync(); // success -> Rebuild #1
        await service.RefreshAsync(); // failure -> Rebuild #2 from cached fallback

        // Rebuilt exactly twice, and the fallback rebuild used the cached posts.
        indexer.Verify(i => i.Rebuild(It.Is<CommunityBlogsData>(d => d.Posts.Count > 0)), Times.Exactly(2));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }
}

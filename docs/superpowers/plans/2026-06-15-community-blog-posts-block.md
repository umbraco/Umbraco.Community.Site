# Community Blog Posts Block Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Block Grid block that renders a full, server-paginated listing of Umbraco community blog posts sourced from the Umbraco Sphere public API, reusing the existing blog-card visual language and adding an author-avatar badge.

**Architecture:** A self-contained sub-feature under `Features/Feeds/CommunityBlogs/` (mirroring the existing `Features/Feeds/Calendar/` feed-backed block). A typed `HttpClient` calls the cursor-paginated Sphere API; a `BackgroundService` walks all cursors every 6h and an `ICommunityBlogsService` persists the result to a TEMP JSON file with memory + stale-disk fallback. The Razor block reads the cached data, offset-slices it for `?page=N`, and renders cards.

**Tech Stack:** ASP.NET Core / .NET 10, Umbraco CMS 17, `System.Text.Json`, xUnit + FluentAssertions + Moq (existing `tests/UmbracoCommunity.Web.Tests`), PostCSS.

---

## Reference files (read before starting)

The new feature mirrors these existing files almost exactly — read them first:

- `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedService.cs` — memory + stale fallback pattern.
- `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedHttpClient.cs` — typed client marker.
- `src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs` — DI composer being extended.
- `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/UpcomingEventsBlock.cshtml` — feed-backed block view template.
- `src/UmbracoCommunity.Web.UI/Views/BlockPreviewApi/BlockGrid/UpcomingEventsBlock.cshtml` — backoffice preview template.
- `src/UmbracoCommunity.Web/Models/ContentModels/UpcomingEventsBlock.cs` — content-model partial template.
- `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/CalendarFeedServiceTests.cs` — `StubHandler`, `FixedTimeProvider`, `TestOptionsMonitor` test helpers (defined at bottom of that file).
- `src/UmbracoCommunity.StaticAssets/src/css/blocks/blog-showcase-block.css` — card styling to mirror.
- `docs/BUILDING_BLOCKS.md` — backoffice element-type creation steps.

## File structure

**New (backend, `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/`):**
- `CommunityBlogsOptions.cs` — bound config.
- `SphereBlogPostsDtos.cs` — API DTOs (`PostsResponseDto`/`PublicPostDto`/`PublicAuthorDto`/`PaginationDto`) + JSON options.
- `CommunityBlogPost.cs` — domain model + `CommunityBlogsData` + `PagedCommunityBlogPosts`.
- `SphereHttpClient.cs` — typed `HttpClient` marker.
- `SphereApiClient.cs` — one `GetBlogPostsAsync` call (sets `Authorization` header).
- `CommunityBlogsAggregator.cs` — walks cursors, maps, sorts; pure (no disk).
- `ICommunityBlogsService.cs` / `CommunityBlogsService.cs` — refresh + cache/disk/stale + `GetPage`.
- `CommunityBlogsBackgroundService.cs` — startup + 6h `PeriodicTimer`.

**Modified (backend):**
- `src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs` — register the above.
- `src/UmbracoCommunity.Web.UI/appsettings.json` — add `CommunityBlogs` section (empty `ApiKey`).
- `src/UmbracoCommunity.Web.UI/appsettings.Local.json` — real `ApiKey` (gitignored, manual).

**New (block + frontend):**
- `src/UmbracoCommunity.Web/Models/ContentModels/CommunityBlogPostsBlock.cs` — `IdHash` partial.
- `src/UmbracoCommunity.Web/Models/PublishedModels/CommunityBlogPostsBlock.generated.cs` — Models Builder output.
- `src/UmbracoCommunity.Web/Models/PublishedModels/SettingsCommunityBlogPostsBlock.generated.cs` — Models Builder output.
- `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml` — front-end view.
- `src/UmbracoCommunity.Web.UI/Views/BlockPreviewApi/BlockGrid/CommunityBlogPostsBlock.cshtml` — preview view.
- `src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css` — styling.

**Modified (frontend):**
- `src/UmbracoCommunity.StaticAssets/src/css/blocks/blocks.css` — `@import` the new CSS.

**New (tests, `tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/`):**
- `CommunityBlogsTestHelpers.cs` — local `StubHandler` (the Calendar one is `internal` to its own namespace).
- `CommunityBlogsAggregatorTests.cs`
- `CommunityBlogsServiceTests.cs`

---

## Task 1: Configuration options + appsettings

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsOptions.cs`
- Modify: `src/UmbracoCommunity.Web.UI/appsettings.json` (after the `CalendarFeed` block, ~line 97-100)

- [ ] **Step 1: Create the options class**

```csharp
namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public class CommunityBlogsOptions
{
    public const string SectionName = "CommunityBlogs";

    /// <summary>Base URL of the Umbraco Sphere public API (must end with a trailing slash).</summary>
    public string ApiBaseUrl { get; set; } = "https://sphere.umbraco.com/api/v1/";

    /// <summary>API key sent in the <c>Authorization</c> header (bare key, no "Bearer"). Supplied via appsettings.Local.json / env — never committed.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>How often the background service re-aggregates posts.</summary>
    public int RefreshIntervalInHours { get; set; } = 6;

    /// <summary>Per-request HTTP timeout.</summary>
    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>Page size requested from the API (max 100).</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>Safety cap on the total number of posts collected per refresh.</summary>
    public int MaxPosts { get; set; } = 1000;
}
```

- [ ] **Step 2: Add the config section to `appsettings.json`**

Insert immediately after the `"CalendarFeed": { ... }` object (keep the trailing comma valid):

```json
  "CommunityBlogs": {
    "ApiBaseUrl": "https://sphere.umbraco.com/api/v1/",
    "ApiKey": "",
    "RefreshIntervalInHours": 6,
    "RequestTimeoutSeconds": 15,
    "PageSize": 100,
    "MaxPosts": 1000
  },
```

- [ ] **Step 3: Add the API key to `appsettings.Local.json` (gitignored, do NOT commit)**

If the file does not exist, create it. Add/merge:

```json
{
  "CommunityBlogs": {
    "ApiKey": "psk_4462e882ba13d8f35c6c2d509564a3f4"
  }
}
```

Note: `appsettings.Local.json` only loads under the `Local` launch profile — run that profile when testing locally. Verify it is gitignored:

Run: `git check-ignore src/UmbracoCommunity.Web.UI/appsettings.Local.json`
Expected: prints the path (i.e. it is ignored).

- [ ] **Step 4: Commit (only the tracked files)**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsOptions.cs src/UmbracoCommunity.Web.UI/appsettings.json
git commit -m "feat(community-blogs): add options and appsettings section"
```

---

## Task 2: API DTOs and domain models

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/SphereBlogPostsDtos.cs`
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogPost.cs`

- [ ] **Step 1: Create the API DTOs**

```csharp
using System.Text.Json;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>Response envelope from GET /v1/blog-posts.</summary>
public sealed record PostsResponseDto(
    IReadOnlyList<PublicPostDto> Data,
    PaginationDto Pagination);

public sealed record PublicPostDto(
    string Id,
    string Type,
    string? Title,
    string? Url,
    string? Content,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    PublicAuthorDto? Author);

public sealed record PublicAuthorDto(
    string? Name,
    string? ProfileUrl,
    string? AvatarUrl);

public sealed record PaginationDto(
    string? NextCursor,
    bool HasMore);

public static class SphereJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}
```

- [ ] **Step 2: Create the domain models**

```csharp
namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>A community blog post, mapped from the Sphere API for rendering.</summary>
public sealed record CommunityBlogPost(
    string Id,
    string Title,
    string Url,
    string? Excerpt,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    string? AuthorName,
    string? AuthorAvatarUrl,
    string? AuthorProfileUrl);

/// <summary>The aggregated, ordered set of posts plus when it was built.</summary>
public sealed record CommunityBlogsData(
    IReadOnlyList<CommunityBlogPost> Posts,
    DateTimeOffset LastUpdatedUtc)
{
    public static CommunityBlogsData Empty { get; } =
        new(Array.Empty<CommunityBlogPost>(), DateTimeOffset.MinValue);
}

/// <summary>One page of posts for the listing view.</summary>
public sealed record PagedCommunityBlogPosts(
    IReadOnlyList<CommunityBlogPost> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/UmbracoCommunity.Web/UmbracoCommunity.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/SphereBlogPostsDtos.cs src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogPost.cs
git commit -m "feat(community-blogs): add API DTOs and domain models"
```

---

## Task 3: Sphere API client

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/SphereHttpClient.cs`
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/SphereApiClient.cs`
- Create: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/CommunityBlogsTestHelpers.cs`
- Test: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/CommunityBlogsAggregatorTests.cs` (created in Task 4; the client is exercised through it and via a direct header test here)

- [ ] **Step 1: Create the typed client marker**

```csharp
namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Marker type for the named HttpClient used by <see cref="SphereApiClient"/>.
/// Configured in <c>RegisterFeeds</c>.
/// </summary>
public sealed class SphereHttpClient
{
    public HttpClient Client { get; }

    public SphereHttpClient(HttpClient client) => Client = client;
}
```

- [ ] **Step 2: Create the API client**

```csharp
using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public sealed class SphereApiClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;

    public SphereApiClient(SphereHttpClient typedClient, IOptionsMonitor<CommunityBlogsOptions> options)
    {
        _http = typedClient.Client;
        _options = options;
    }

    /// <summary>Fetches one page of blog posts. <paramref name="cursor"/> null fetches the first page.</summary>
    public async Task<PostsResponseDto?> GetBlogPostsAsync(string? cursor, int limit, CancellationToken cancellationToken)
    {
        var requestUri = $"blog-posts?limit={limit.ToString(CultureInfo.InvariantCulture)}";
        if (!string.IsNullOrEmpty(cursor))
        {
            requestUri += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("Authorization", _options.CurrentValue.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PostsResponseDto>(SphereJsonOptions.Default, cancellationToken);
    }
}
```

- [ ] **Step 3: Create the test helpers (local StubHandler)**

```csharp
using System.Net;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

/// <summary>Records requests and returns scripted responses.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = new();
    public int CallCount => Requests.Count;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public static StubHandler Json(string body) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    });

    public static StubHandler Throws() => new(_ => throw new HttpRequestException("simulated network error"));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; set; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public FixedTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
```

- [ ] **Step 4: Write a failing test asserting the Authorization header + cursor URL**

Create `tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/CommunityBlogsAggregatorTests.cs` with this first test (more tests added in Task 4):

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

public class CommunityBlogsAggregatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-15T10:00:00Z");

    private static string Page(string id, string publishedAt, string? nextCursor, bool hasMore) => $$"""
    {
      "data": [
        {
          "id": "{{id}}",
          "type": "blog_post",
          "title": "Post {{id}}",
          "url": "https://blog.example/{{id}}",
          "content": "Excerpt {{id}}",
          "coverImageUrl": "https://blog.example/{{id}}.png",
          "publishedAt": "{{publishedAt}}",
          "author": { "name": "Author {{id}}", "profileUrl": "https://blog.example", "avatarUrl": "https://blog.example/avatar.png" }
        }
      ],
      "pagination": { "nextCursor": {{(nextCursor is null ? "null" : $"\"{nextCursor}\"")}}, "hasMore": {{(hasMore ? "true" : "false")}} }
    }
    """;

    private static (SphereApiClient Client, StubHandler Handler) CreateClient(
        StubHandler handler, CommunityBlogsOptions options)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local/api/v1/") };
        var typed = new SphereHttpClient(http);
        var client = new SphereApiClient(typed, new TestOptionsMonitor<CommunityBlogsOptions>(options));
        return (client, handler);
    }

    [Fact]
    public async Task Client_sends_authorization_header_and_cursor()
    {
        var handler = StubHandler.Json(Page("a", "2026-06-10T00:00:00Z", null, false));
        var (client, h) = CreateClient(handler, new CommunityBlogsOptions { ApiKey = "psk_test" });

        await client.GetBlogPostsAsync("CUR123", 50, CancellationToken.None);

        var request = h.Requests.Single();
        request.Headers.GetValues("Authorization").Single().Should().Be("psk_test");
        request.RequestUri!.Query.Should().Contain("limit=50").And.Contain("cursor=CUR123");
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/UmbracoCommunity.Web.Tests --filter "FullyQualifiedName~CommunityBlogsAggregatorTests.Client_sends_authorization_header_and_cursor"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/SphereHttpClient.cs src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/SphereApiClient.cs tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/
git commit -m "feat(community-blogs): add Sphere API client with auth header test"
```

---

## Task 4: Aggregator (cursor walking + mapping)

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsAggregator.cs`
- Test: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/CommunityBlogsAggregatorTests.cs` (append tests)

- [ ] **Step 1: Write failing tests for mapping, multi-page walk, cap, and first-page failure**

Append these tests inside the `CommunityBlogsAggregatorTests` class (the `Page`, `Now`, helpers already exist):

```csharp
    private static CommunityBlogsAggregator CreateAggregator(StubHandler handler, CommunityBlogsOptions options)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.local/api/v1/") };
        var client = new SphereApiClient(new SphereHttpClient(http), new TestOptionsMonitor<CommunityBlogsOptions>(options));
        return new CommunityBlogsAggregator(
            client,
            new TestOptionsMonitor<CommunityBlogsOptions>(options),
            new FixedTimeProvider(Now),
            NullLogger<CommunityBlogsAggregator>.Instance);
    }

    [Fact]
    public async Task Maps_fields_and_orders_newest_first()
    {
        // page 1 -> hasMore -> page 2 (end). Older first in the wire order to prove re-sort.
        var responder = new Func<HttpRequestMessage, HttpResponseMessage>(req =>
        {
            var body = req.RequestUri!.Query.Contains("cursor=NEXT")
                ? Page("new", "2026-06-12T00:00:00Z", null, false)
                : Page("old", "2026-06-01T00:00:00Z", "NEXT", true);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var aggregator = CreateAggregator(new StubHandler(responder),
            new CommunityBlogsOptions { ApiKey = "psk_test", PageSize = 1, MaxPosts = 100 });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data.Should().NotBeNull();
        data!.Posts.Should().HaveCount(2);
        data.Posts[0].Id.Should().Be("new"); // newest first
        data.Posts[0].Title.Should().Be("Post new");
        data.Posts[0].Url.Should().Be("https://blog.example/new");
        data.Posts[0].Excerpt.Should().Be("Excerpt new");
        data.Posts[0].CoverImageUrl.Should().Be("https://blog.example/new.png");
        data.Posts[0].AuthorName.Should().Be("Author new");
        data.Posts[0].AuthorAvatarUrl.Should().Be("https://blog.example/avatar.png");
        data.LastUpdatedUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Stops_at_MaxPosts()
    {
        // Every page says hasMore with a unique cursor, so only the cap halts the walk.
        var counter = 0;
        var responder = new Func<HttpRequestMessage, HttpResponseMessage>(_ =>
        {
            counter++;
            var body = Page($"p{counter}", "2026-06-01T00:00:00Z", $"C{counter}", true);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        });
        var aggregator = CreateAggregator(new StubHandler(responder),
            new CommunityBlogsOptions { ApiKey = "psk_test", PageSize = 1, MaxPosts = 3 });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data!.Posts.Should().HaveCount(3);
    }

    [Fact]
    public async Task Returns_null_when_first_page_fails()
    {
        var aggregator = CreateAggregator(StubHandler.Throws(),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data.Should().BeNull(); // signals "keep existing data"
    }

    [Fact]
    public async Task Returns_null_when_api_key_missing()
    {
        var aggregator = CreateAggregator(StubHandler.Json(Page("a", "2026-06-01T00:00:00Z", null, false)),
            new CommunityBlogsOptions { ApiKey = "" });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data.Should().BeNull();
    }

    [Fact]
    public async Task Skips_items_without_url_or_title()
    {
        const string body = """
        {
          "data": [
            { "id": "1", "type": "blog_post", "title": "Good", "url": "https://x/1", "content": null, "coverImageUrl": null, "publishedAt": "2026-06-01T00:00:00Z", "author": null },
            { "id": "2", "type": "blog_post", "title": "No URL", "url": null, "content": null, "coverImageUrl": null, "publishedAt": "2026-06-02T00:00:00Z", "author": null }
          ],
          "pagination": { "nextCursor": null, "hasMore": false }
        }
        """;
        var aggregator = CreateAggregator(StubHandler.Json(body),
            new CommunityBlogsOptions { ApiKey = "psk_test" });

        var data = await aggregator.BuildAsync(CancellationToken.None);

        data!.Posts.Should().ContainSingle().Which.Id.Should().Be("1");
        data.Posts[0].Excerpt.Should().BeNull();
        data.Posts[0].AuthorName.Should().BeNull();
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/UmbracoCommunity.Web.Tests --filter "FullyQualifiedName~CommunityBlogsAggregatorTests"`
Expected: FAIL with "CommunityBlogsAggregator could not be found" / does not contain `BuildAsync`.

- [ ] **Step 3: Implement the aggregator**

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Walks the cursor-paginated Sphere blog-posts API, maps to <see cref="CommunityBlogPost"/>,
/// and returns the newest-first set. Returns null when nothing could be fetched (so callers
/// keep any previously-persisted data).
/// </summary>
public sealed class CommunityBlogsAggregator
{
    private readonly SphereApiClient _client;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<CommunityBlogsAggregator> _logger;

    public CommunityBlogsAggregator(
        SphereApiClient client,
        IOptionsMonitor<CommunityBlogsOptions> options,
        TimeProvider time,
        ILogger<CommunityBlogsAggregator> logger)
    {
        _client = client;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<CommunityBlogsData?> BuildAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            _logger.LogWarning("CommunityBlogsOptions.ApiKey is not configured; skipping refresh.");
            return null;
        }

        var posts = new List<CommunityBlogPost>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        var anySuccess = false;

        while (posts.Count < options.MaxPosts)
        {
            PostsResponseDto? page;
            try
            {
                page = await _client.GetBlogPostsAsync(cursor, options.PageSize, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch a page of community blog posts (cursor={Cursor}).", cursor);
                break;
            }

            if (page is null)
            {
                break;
            }

            anySuccess = true;

            foreach (var dto in page.Data)
            {
                var mapped = Map(dto);
                if (mapped is not null)
                {
                    posts.Add(mapped);
                }

                if (posts.Count >= options.MaxPosts)
                {
                    break;
                }
            }

            if (!page.Pagination.HasMore || string.IsNullOrEmpty(page.Pagination.NextCursor))
            {
                break;
            }

            if (!seenCursors.Add(page.Pagination.NextCursor))
            {
                _logger.LogWarning("Repeated cursor {Cursor} from Sphere API; stopping walk.", page.Pagination.NextCursor);
                break;
            }

            cursor = page.Pagination.NextCursor;
        }

        if (!anySuccess)
        {
            return null;
        }

        var ordered = posts
            .OrderByDescending(p => p.PublishedAt)
            .Take(options.MaxPosts)
            .ToArray();

        return new CommunityBlogsData(ordered, _time.GetUtcNow());
    }

    internal static CommunityBlogPost? Map(PublicPostDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Url) || string.IsNullOrWhiteSpace(dto.Title))
        {
            return null;
        }

        return new CommunityBlogPost(
            Id: dto.Id,
            Title: dto.Title!.Trim(),
            Url: dto.Url!.Trim(),
            Excerpt: string.IsNullOrWhiteSpace(dto.Content) ? null : dto.Content!.Trim(),
            CoverImageUrl: string.IsNullOrWhiteSpace(dto.CoverImageUrl) ? null : dto.CoverImageUrl!.Trim(),
            PublishedAt: dto.PublishedAt,
            AuthorName: string.IsNullOrWhiteSpace(dto.Author?.Name) ? null : dto.Author!.Name!.Trim(),
            AuthorAvatarUrl: string.IsNullOrWhiteSpace(dto.Author?.AvatarUrl) ? null : dto.Author!.AvatarUrl!.Trim(),
            AuthorProfileUrl: string.IsNullOrWhiteSpace(dto.Author?.ProfileUrl) ? null : dto.Author!.ProfileUrl!.Trim());
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/UmbracoCommunity.Web.Tests --filter "FullyQualifiedName~CommunityBlogsAggregatorTests"`
Expected: PASS (all 6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsAggregator.cs tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/CommunityBlogsAggregatorTests.cs
git commit -m "feat(community-blogs): add cursor-walking aggregator"
```

---

## Task 5: Service (cache + disk + pagination)

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/ICommunityBlogsService.cs`
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsService.cs`
- Test: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/CommunityBlogsServiceTests.cs`

- [ ] **Step 1: Create the interface**

```csharp
namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public interface ICommunityBlogsService
{
    /// <summary>Re-aggregates posts and persists them (memory + disk). No-ops if nothing could be fetched.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the current data from memory, falling back to the disk cache, then stale, then empty.</summary>
    CommunityBlogsData GetData();

    /// <summary>Returns one page of posts (1-based, clamped to the valid range).</summary>
    PagedCommunityBlogPosts GetPage(int page, int pageSize);
}
```

- [ ] **Step 2: Write failing tests**

```csharp
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

    private CommunityBlogsService CreateService(SphereApiClient client, CommunityBlogsOptions options)
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
            new MemoryCache(new MemoryCacheOptions()),
            new TestOptionsMonitor<CommunityBlogsOptions>(options),
            env.Object,
            NullLogger<CommunityBlogsService>.Instance);
    }

    private static SphereApiClient ClientReturning(string json)
    {
        var http = new HttpClient(StubHandler.Json(json)) { BaseAddress = new Uri("https://test.local/api/v1/") };
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

        // Swap in a failing client by building a fresh service over the SAME temp root (reads disk).
        var failing = new HttpClient(StubHandler.Throws()) { BaseAddress = new Uri("https://test.local/api/v1/") };
        var failingClient = new SphereApiClient(new SphereHttpClient(failing), new TestOptionsMonitor<CommunityBlogsOptions>(new()));
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempRoot);
        var aggregator = new CommunityBlogsAggregator(failingClient,
            new TestOptionsMonitor<CommunityBlogsOptions>(new CommunityBlogsOptions { ApiKey = "psk_test" }),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-06-15T10:00:00Z")),
            NullLogger<CommunityBlogsAggregator>.Instance);
        var service2 = new CommunityBlogsService(aggregator, new MemoryCache(new MemoryCacheOptions()),
            new TestOptionsMonitor<CommunityBlogsOptions>(new CommunityBlogsOptions { ApiKey = "psk_test" }),
            env.Object, NullLogger<CommunityBlogsService>.Instance);

        await service2.RefreshAsync(); // fails -> must not wipe disk

        service2.GetData().Posts.Should().HaveCount(5); // read back from disk
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/UmbracoCommunity.Web.Tests --filter "FullyQualifiedName~CommunityBlogsServiceTests"`
Expected: FAIL — `CommunityBlogsService` does not exist.

- [ ] **Step 4: Implement the service**

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public sealed class CommunityBlogsService : ICommunityBlogsService
{
    private const string PrimaryCacheKey = "community-blogs:primary";
    private const string StaleCacheKey = "community-blogs:stale";
    private static readonly TimeSpan StaleFallbackDuration = TimeSpan.FromDays(30);

    private readonly CommunityBlogsAggregator _aggregator;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly ILogger<CommunityBlogsService> _logger;
    private readonly string _cacheFilePath;

    public CommunityBlogsService(
        CommunityBlogsAggregator aggregator,
        IMemoryCache cache,
        IOptionsMonitor<CommunityBlogsOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<CommunityBlogsService> logger)
    {
        _aggregator = aggregator;
        _cache = cache;
        _options = options;
        _logger = logger;

        var cacheDir = Path.Combine(hostEnvironment.ContentRootPath, "umbraco", "Data", "TEMP", "CommunityBlogsCache");
        Directory.CreateDirectory(cacheDir);
        _cacheFilePath = Path.Combine(cacheDir, "community-blogs.json");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var data = await _aggregator.BuildAsync(cancellationToken);
        if (data is null)
        {
            _logger.LogInformation("Community blogs refresh produced no data; keeping existing cache.");
            return;
        }

        var primaryDuration = TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RefreshIntervalInHours));
        _cache.Set(PrimaryCacheKey, data, primaryDuration);
        _cache.Set(StaleCacheKey, data, new MemoryCacheEntryOptions { SlidingExpiration = StaleFallbackDuration });

        await WriteCacheFileAsync(data, cancellationToken);
        _logger.LogInformation("Refreshed {Count} community blog posts.", data.Posts.Count);
    }

    public CommunityBlogsData GetData()
    {
        if (_cache.TryGetValue(PrimaryCacheKey, out CommunityBlogsData? primary) && primary is not null)
        {
            return primary;
        }

        var disk = TryReadCacheFile();
        if (disk is not null)
        {
            _cache.Set(StaleCacheKey, disk, new MemoryCacheEntryOptions { SlidingExpiration = StaleFallbackDuration });
            return disk;
        }

        if (_cache.TryGetValue(StaleCacheKey, out CommunityBlogsData? stale) && stale is not null)
        {
            return stale;
        }

        return CommunityBlogsData.Empty;
    }

    public PagedCommunityBlogPosts GetPage(int page, int pageSize)
    {
        var data = GetData();
        pageSize = Math.Max(1, pageSize);

        var totalItems = data.Posts.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var clampedPage = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);

        var items = data.Posts
            .Skip((clampedPage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedCommunityBlogPosts(items, clampedPage, pageSize, totalItems, totalPages);
    }

    private async Task WriteCacheFileAsync(CommunityBlogsData data, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, SphereJsonOptions.Default);
            await File.WriteAllTextAsync(_cacheFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write community blogs disk cache to {Path}.", _cacheFilePath);
        }
    }

    private CommunityBlogsData? TryReadCacheFile()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_cacheFilePath);
            return JsonSerializer.Deserialize<CommunityBlogsData>(json, SphereJsonOptions.Default);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read community blogs disk cache from {Path}.", _cacheFilePath);
            return null;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/UmbracoCommunity.Web.Tests --filter "FullyQualifiedName~CommunityBlogsServiceTests"`
Expected: PASS (all 5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/ICommunityBlogsService.cs src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsService.cs tests/UmbracoCommunity.Web.Tests/Features/Feeds/CommunityBlogs/CommunityBlogsServiceTests.cs
git commit -m "feat(community-blogs): add caching service with pagination"
```

---

## Task 6: Background refresh service

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsBackgroundService.cs`

- [ ] **Step 1: Implement the background service**

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>Aggregates community blog posts on startup and then on a periodic timer.</summary>
public sealed class CommunityBlogsBackgroundService : BackgroundService
{
    private readonly ICommunityBlogsService _service;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly ILogger<CommunityBlogsBackgroundService> _logger;

    public CommunityBlogsBackgroundService(
        ICommunityBlogsService service,
        IOptionsMonitor<CommunityBlogsOptions> options,
        ILogger<CommunityBlogsBackgroundService> logger)
    {
        _service = service;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshSafelyAsync(stoppingToken);

        var interval = TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RefreshIntervalInHours));
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _service.RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while refreshing community blog posts.");
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/UmbracoCommunity.Web/UmbracoCommunity.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsBackgroundService.cs
git commit -m "feat(community-blogs): add background refresh service"
```

---

## Task 7: Register everything in RegisterFeeds

**Files:**
- Modify: `src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs`

- [ ] **Step 1: Add the registrations**

Add `using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;` at the top, then inside `Compose`, after the existing Calendar registrations (before the closing brace), add:

```csharp
        // --- Community blog posts (Umbraco Sphere API) ---
        builder.Services.Configure<CommunityBlogsOptions>(
            builder.Config.GetSection(CommunityBlogsOptions.SectionName));

        var communityBlogsOptions =
            builder.Config.GetSection(CommunityBlogsOptions.SectionName).Get<CommunityBlogsOptions>()
            ?? new CommunityBlogsOptions();

        builder.Services.AddHttpClient<SphereHttpClient>(client =>
        {
            client.BaseAddress = new Uri(communityBlogsOptions.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, communityBlogsOptions.RequestTimeoutSeconds));
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.Services.AddSingleton<SphereApiClient>();
        builder.Services.AddSingleton<CommunityBlogsAggregator>();
        builder.Services.AddSingleton<ICommunityBlogsService, CommunityBlogsService>();
        builder.Services.AddHostedService<CommunityBlogsBackgroundService>();
```

Note: `TimeProvider.System` is already registered earlier in this composer (for the Calendar feed) — do **not** add it again.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/UmbracoCommunity.Web/UmbracoCommunity.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs
git commit -m "feat(community-blogs): register services and background worker"
```

---

## Task 8: Create the block document types + generate models

This task uses the Umbraco backoffice (no uSync in this repo) and mirrors `upcomingEventsBlock` / `settingsUpcomingEventsBlock` exactly. Run the site under the `Local` profile so `appsettings.Local.json` (the API key) loads:

```bash
cd src/UmbracoCommunity.Web.UI
dotnet run --launch-profile Local
```

- [ ] **Step 1: Create the content element type**

In **Settings → Document Types**, create a new **Element Type**:
- Name: `Community Blog Posts Block`
- Alias: `communityBlogPostsBlock`
- Icon: pick any (e.g. `icon-newspaper`)
- **Compositions:** add `Content Block Intro` (alias `contentBlockIntro`) — this provides `Title` + `Subtitle`. No other properties needed.

- [ ] **Step 2: Create the settings element type**

Create another **Element Type**:
- Name: `Settings Community Blog Posts Block`
- Alias: `settingsCommunityBlogPostsBlock`
- **Compositions:** add `Settings Block Id` (`settingsBlockId`) and `Settings Colour` (`settingsColour`).
- Add one property:
  - Label: `Posts per page`
  - Alias: `postsPerPage`
  - Editor: **Numeric** (integer)
  - Default/description: "How many posts to show per page (default 12)."

- [ ] **Step 3: Add the block to the content Block Grid data type**

Open the Block Grid data type used by content pages (the same one that lists `Blog Showcase Block` / `Upcoming Events Block` — find it under **Settings → Data Types**). Add a new block:
- Content element type: `communityBlogPostsBlock`
- Settings element type: `settingsCommunityBlogPostsBlock`
- Label / preview: match the conventions of the neighbouring blocks.

- [ ] **Step 4: Generate Models Builder classes**

In **Settings → Models Builder**, click **Generate models**. Confirm two files appeared:
- `src/UmbracoCommunity.Web/Models/PublishedModels/CommunityBlogPostsBlock.generated.cs` (implements `IContentBlockIntro`)
- `src/UmbracoCommunity.Web/Models/PublishedModels/SettingsCommunityBlogPostsBlock.generated.cs` (implements `ISettingsBlockId`, `ISettingsColour`, exposes `PostsPerPage`)

- [ ] **Step 5: Add the hand-written content-model partial**

Create `src/UmbracoCommunity.Web/Models/ContentModels/CommunityBlogPostsBlock.cs`:

```csharp
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class CommunityBlogPostsBlock
    {
        private const int DefaultPostsPerPage = 12;

        public string IdHash { get; } = StringUtilities.RandomString(5);

        public int ResolvedPostsPerPage =>
            SettingsPostsPerPage is > 0 ? SettingsPostsPerPage : DefaultPostsPerPage;

        // PostsPerPage lives on the settings element; the view passes it in. This helper is a
        // fallback used only if a caller has the content model but not the settings model.
        public int SettingsPostsPerPage { get; set; }
    }
}
```

Note: the per-page value really comes from the **settings** model (`SettingsCommunityBlogPostsBlock.PostsPerPage`); the view reads it directly (Task 9). `ResolvedPostsPerPage` is not required by the view — if you prefer, omit this partial entirely and keep only `IdHash`. Minimal version:

```csharp
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class CommunityBlogPostsBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
```

Use the **minimal version** (the view computes the page size from settings with its own default).

- [ ] **Step 6: Build to verify generated + partial compile**

Run: `dotnet build src/UmbracoCommunity.Web/UmbracoCommunity.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/UmbracoCommunity.Web/Models/PublishedModels/CommunityBlogPostsBlock.generated.cs src/UmbracoCommunity.Web/Models/PublishedModels/SettingsCommunityBlogPostsBlock.generated.cs src/UmbracoCommunity.Web/Models/ContentModels/CommunityBlogPostsBlock.cs
git commit -m "feat(community-blogs): add block document types and models"
```

---

## Task 9: Front-end block view

**Files:**
- Create: `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml`

- [ ] **Step 1: Create the view**

```cshtml
@using System.Globalization
@using Microsoft.AspNetCore.WebUtilities
@using Umbraco.Cms.Core.Models.Blocks
@using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs
@using UmbracoCommunity.Web.Models.PublishedModels
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<BlockGridItem<CommunityBlogPostsBlock, SettingsCommunityBlogPostsBlock>>
@inject ICommunityBlogsService CommunityBlogsService

@{
    const int DefaultPostsPerPage = 12;
    var settingsPerPage = Model.Settings?.PostsPerPage ?? 0;
    var pageSize = settingsPerPage > 0 ? settingsPerPage : DefaultPostsPerPage;

    var requestedPage = 1;
    if (int.TryParse(Context.Request.Query["page"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
    {
        requestedPage = parsed;
    }

    var result = CommunityBlogsService.GetPage(requestedPage, pageSize);

    var hasBg = Model.Settings.HasBg();
    var bgIsDark = hasBg && Model.Settings.IsDark();
    var anchor = !string.IsNullOrEmpty(Model.Settings?.BlockId) ? Model.Settings!.BlockId : Model.Content.IdHash;
}

@functions {
    string BuildPageUrl(int targetPage, string anchorId)
    {
        var request = Context.Request;
        var query = QueryHelpers.ParseQuery(request.QueryString.Value);
        var items = query
            .Where(kvp => !string.Equals(kvp.Key, "page", StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => kvp.Value, (kvp, v) => new KeyValuePair<string, string?>(kvp.Key, v))
            .ToList();
        items.Add(new KeyValuePair<string, string?>("page", targetPage.ToString(CultureInfo.InvariantCulture)));
        var url = QueryHelpers.AddQueryString(request.Path, items);
        return string.IsNullOrEmpty(anchorId) ? url : url + "#" + anchorId;
    }
}

@if (hasBg)
{
    <style asp-add-nonce="true">
        #@Model.Content.IdHash {
            --block-background-color: @Model.Settings?.BackgroundColour?.Color;
        }
    </style>
}

<section class="dc-community-blogs @(hasBg ? "has-bg" : string.Empty) @(bgIsDark ? "bg-dark" : string.Empty)"
         id="@Model.Content.IdHash">
    <div class="dc-community-blogs__intro align-center" id="@(Model.Settings?.BlockId ?? string.Empty)">
        @if (!string.IsNullOrEmpty(Model.Content.Title))
        {
            <h2 class="block-title">@Model.Content.Title</h2>
        }
        @if (!Model.Content.Subtitle.IsEmptyHtmlString())
        {
            <div class="block-subtitle">
                @Html.Raw(Model.Content.Subtitle?.ToHtmlString())
            </div>
        }
    </div>

    @if (result.TotalItems == 0)
    {
        <p class="dc-community-blogs__empty">
            Unfortunately we are currently unable to display the community blog posts. Please try again later.
        </p>
    }
    else
    {
        <div class="dc-community-blogs__grid">
            @foreach (var post in result.Items)
            {
                <a class="dc-community-blogs__card" href="@post.Url" target="_blank" rel="noopener noreferrer">
                    @if (!string.IsNullOrEmpty(post.CoverImageUrl))
                    {
                        <figure class="dc-community-blogs__media">
                            <img src="@post.CoverImageUrl" alt="" loading="lazy" />
                        </figure>
                    }
                    <div class="dc-community-blogs__content">
                        @if (!string.IsNullOrEmpty(post.AuthorAvatarUrl))
                        {
                            <img class="dc-community-blogs__avatar" src="@post.AuthorAvatarUrl" alt="" loading="lazy" />
                        }
                        <h3 class="dc-community-blogs__title">@post.Title</h3>
                        @if (!string.IsNullOrEmpty(post.Excerpt))
                        {
                            <p class="dc-community-blogs__teaser">@post.Excerpt</p>
                        }
                        <div class="dc-community-blogs__meta">
                            <time datetime="@post.PublishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)">
                                @post.PublishedAt.ToString("d MMMM yyyy", CultureInfo.CurrentCulture)
                            </time>
                            @if (!string.IsNullOrEmpty(post.AuthorName))
                            {
                                <span class="dc-community-blogs__author">by @post.AuthorName</span>
                            }
                        </div>
                    </div>
                </a>
            }
        </div>

        @if (result.TotalPages > 1)
        {
            <nav class="dc-community-blogs__pagination" aria-label="Community blog posts pages">
                @if (result.Page > 1)
                {
                    <a class="dc-community-blogs__page-link" rel="prev" href="@BuildPageUrl(result.Page - 1, anchor)">Previous</a>
                }
                @for (var p = 1; p <= result.TotalPages; p++)
                {
                    if (p == result.Page)
                    {
                        <span class="dc-community-blogs__page-link is-current" aria-current="page">@p</span>
                    }
                    else
                    {
                        <a class="dc-community-blogs__page-link" href="@BuildPageUrl(p, anchor)">@p</a>
                    }
                }
                @if (result.Page < result.TotalPages)
                {
                    <a class="dc-community-blogs__page-link" rel="next" href="@BuildPageUrl(result.Page + 1, anchor)">Next</a>
                }
            </nav>
        }
    }
</section>
```

- [ ] **Step 2: Commit**

```bash
git add src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml
git commit -m "feat(community-blogs): add front-end block view"
```

---

## Task 10: Backoffice preview view

**Files:**
- Create: `src/UmbracoCommunity.Web.UI/Views/BlockPreviewApi/BlockGrid/CommunityBlogPostsBlock.cshtml`

The existing `BlockPreview.BlockGrid.ViewLocations` in `appsettings.json` already points at
`/Views/BlockPreviewApi/BlockGrid/{0}.cshtml`, so no config change is needed — just add the file.

- [ ] **Step 1: Create the preview view**

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<Umbraco.Cms.Core.Models.Blocks.BlockGridItem<CommunityBlogPostsBlock, SettingsCommunityBlogPostsBlock>>

@{
    var hasBg = Model.Settings.HasBg();
    var bgIsDark = hasBg && Model.Settings.IsDark();
    var blockStyle = hasBg ? $"--block-background-color: {Model.Settings?.BackgroundColour?.Color}" : null;
    var perPage = (Model.Settings?.PostsPerPage ?? 0) > 0 ? Model.Settings!.PostsPerPage : 12;
}

<div class="dc-community-blogs-preview @(hasBg ? "has-bg" : string.Empty) @(bgIsDark ? "bg-dark" : string.Empty)" style="@blockStyle">
    <h3>@(string.IsNullOrWhiteSpace(Model.Content.Title) ? "Community Blog Posts" : Model.Content.Title)</h3>
    @if (!Model.Content.Subtitle.IsEmptyHtmlString())
    {
        <div class="dc-community-blogs-preview__subtitle">
            @Html.Raw(Model.Content.Subtitle?.ToHtmlString())
        </div>
    }
    <p class="dc-community-blogs-preview__note">
        Live preview shows community blog posts from
        <a href="https://sphere.umbraco.com" rel="noopener" target="_blank">Umbraco Sphere</a>,
        paginated at <strong>@perPage</strong> per page on the front end.
    </p>
</div>
```

- [ ] **Step 2: Commit**

```bash
git add src/UmbracoCommunity.Web.UI/Views/BlockPreviewApi/BlockGrid/CommunityBlogPostsBlock.cshtml
git commit -m "feat(community-blogs): add backoffice block preview"
```

---

## Task 11: Styling

**Files:**
- Create: `src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css`
- Modify: `src/UmbracoCommunity.StaticAssets/src/css/blocks/blocks.css`

- [ ] **Step 1: Create the CSS (reuses the blog-showcase card tokens/look + avatar badge + pagination)**

```css
.dc-community-blogs {
  --cb-card-bg: var(--color-white);
  --cb-card-fg: var(--color-dark);
  --cb-card-radius: 20px;
  max-width: var(--max-width);
  margin: 0 auto;
}

.dc-community-blogs.has-bg {
  background-color: var(--block-background-color);
  padding: var(--unit-lg) var(--unit) var(--unit-md);
  border-radius: var(--border-radius-lg);
}

.dc-community-blogs.bg-dark,
.dc-community-blogs.bg-dark .block-title,
.dc-community-blogs.bg-dark .block-subtitle {
  color: var(--color-white);
}

.dc-community-blogs__intro {
  text-align: center;
}

.dc-community-blogs__empty {
  text-align: center;
  margin-top: var(--unit-md);
}

.dc-community-blogs__grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.25rem;
  margin-top: var(--unit-md);
}

.dc-community-blogs__card {
  position: relative;
  display: flex;
  flex-direction: column;
  background: var(--cb-card-bg);
  color: var(--cb-card-fg);
  border-radius: var(--cb-card-radius);
  overflow: hidden;
  text-decoration: none;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
  transition: box-shadow 200ms ease;
  height: 100%;
}

.dc-community-blogs__card:hover {
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.12);
}

.dc-community-blogs__card:focus-visible {
  outline: var(--focus-outline);
  outline-offset: var(--focus-outline-offset);
}

.dc-community-blogs__media {
  margin: 0;
  aspect-ratio: 16 / 9;
  overflow: hidden;
  background: var(--color-light);
}

.dc-community-blogs__media img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
  transition: transform 400ms ease;
}

.dc-community-blogs__card:hover .dc-community-blogs__media img {
  transform: scale(1.06);
}

.dc-community-blogs__content {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 1.25rem 1.5rem 1.5rem;
  flex: 1;
}

/* Author avatar "owner" badge, overlapping the media/content boundary. */
.dc-community-blogs__avatar {
  position: absolute;
  top: -22px;
  right: 1.25rem;
  width: 44px;
  height: 44px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--color-white);
  border: 2px solid var(--color-white);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);
}

/* When there is no cover image, the avatar sits inside the content flow instead of overlapping. */
.dc-community-blogs__card:not(:has(.dc-community-blogs__media)) .dc-community-blogs__avatar {
  position: static;
  margin-bottom: 0.25rem;
}

.dc-community-blogs__title {
  margin: 0;
  font-size: 22px;
  line-height: 1.3;
  color: var(--color-blue);
}

.dc-community-blogs__teaser {
  margin: 0;
  font-size: 16px;
  line-height: 1.5;
  color: var(--color-light-blue);
  flex: 1;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.dc-community-blogs__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.75rem;
  margin-top: 0.5rem;
  font-size: 14px;
  color: var(--color-dark-grey);
}

/* Pagination */
.dc-community-blogs__pagination {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.5rem;
  margin-top: var(--unit-lg);
}

.dc-community-blogs__page-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 40px;
  padding: 0.4rem 0.75rem;
  border-radius: 8px;
  text-decoration: none;
  color: var(--color-blue);
  border: 1px solid var(--color-light);
}

.dc-community-blogs__page-link.is-current {
  background: var(--color-blue);
  color: var(--color-white);
  border-color: var(--color-blue);
}

.dc-community-blogs.bg-dark .dc-community-blogs__page-link {
  color: var(--color-white);
  border-color: var(--color-white);
}

@media (--sm) {
  .dc-community-blogs.has-bg {
    padding: var(--unit-xl) var(--unit-md) var(--unit-md);
  }

  .dc-community-blogs__grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (--lg) {
  .dc-community-blogs__grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 1.75rem;
    margin-top: var(--unit-lg);
  }
}
```

- [ ] **Step 2: Register the stylesheet**

In `src/UmbracoCommunity.StaticAssets/src/css/blocks/blocks.css`, add this line next to the other block imports (e.g. right after the `@import "./blog-showcase-block.css";` line):

```css
@import "./community-blogs-block.css";
```

- [ ] **Step 3: Build the frontend to verify CSS compiles**

```bash
cd src/UmbracoCommunity.StaticAssets
npm run build
```
Expected: build completes with no PostCSS errors.

- [ ] **Step 4: Commit**

```bash
git add src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css src/UmbracoCommunity.StaticAssets/src/css/blocks/blocks.css
git commit -m "feat(community-blogs): add block styling"
```

---

## Task 12: Full build, test run, and manual verification

- [ ] **Step 1: Run the full backend test suite**

Run: `dotnet test tests/UmbracoCommunity.Web.Tests`
Expected: PASS (including all new `CommunityBlogs*` tests).

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Run the site under the Local profile (loads the API key) + Vite**

Terminal 1:
```bash
cd src/UmbracoCommunity.Web.UI
dotnet run --launch-profile Local
```
Terminal 2:
```bash
cd src/UmbracoCommunity.StaticAssets
npm run dev
```

- [ ] **Step 4: Manually verify**

- In the backoffice, add the **Community Blog Posts Block** to a content page's Block Grid, set a Title, set Posts per page (e.g. 6), and confirm the **preview** renders the note text.
- Publish, then view the page on the front end. Confirm:
  - Cards render with cover image (where present), author avatar badge, title, excerpt, and `date by author`.
  - Posts are newest-first.
  - Pagination appears when there are more posts than the page size, and `?page=2` shows the next set and keeps you anchored at the block.
  - Outbound links open the original post in a new tab.
- Check application logs for `Refreshed N community blog posts.` on startup.

- [ ] **Step 5: Final commit (if any tweaks were needed)**

```bash
git add -A
git commit -m "feat(community-blogs): finalize block after verification"
```

---

## Self-review notes (for the implementer)

- **API key** is only in `appsettings.Local.json` (gitignored). Never `git add` that file. The committed `appsettings.json` keeps `ApiKey` empty.
- The aggregator returns `null` (not empty data) when the API key is missing or the first page fails, so a transient outage never wipes the disk cache.
- Type names are consistent across tasks: `PostsResponseDto`/`PublicPostDto`/`PublicAuthorDto`/`PaginationDto`, `CommunityBlogPost`/`CommunityBlogsData`/`PagedCommunityBlogPosts`, `SphereHttpClient`/`SphereApiClient`, `CommunityBlogsAggregator.BuildAsync`, `ICommunityBlogsService.{RefreshAsync,GetData,GetPage}`.
- Block element/settings aliases: `communityBlogPostsBlock` / `settingsCommunityBlogPostsBlock`; settings property alias `postsPerPage`.

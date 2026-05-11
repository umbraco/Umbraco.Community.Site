# Calendar Feed Block Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a content block that displays upcoming Umbraco community events from umbracalendar.com (and other matching JSON feeds), with backoffice-configured feeds, server-side caching, and stale-on-error fallback.

**Architecture:** Per-tenant `Settings/Feeds/<calendarFeed>` nodes hold feed URL + cache duration. A `calendarFeedBlock` element type picks one or more feeds + max items. An `ICalendarFeedService` fetches JSON, deserialises to typed records, filters past events, sorts ascending by `StartsAt`, and caches with stale-on-error. The Razor partial injects the service, awaits all picked feeds in parallel, round-robin merges, and renders event cards.

**Tech Stack:** .NET 10, Umbraco CMS 17.3, `System.Text.Json`, `IHttpClientFactory`, `IMemoryCache`, xUnit.

**Spec:** [docs/superpowers/specs/2026-04-29-calendar-feed-block-design.md](../specs/2026-04-29-calendar-feed-block-design.md)

---

## Context for the implementer

You're working on a multi-tenant Umbraco site. Read these files to understand the conventions before starting:

- `src/UmbracoCommunity.Web/Features/Sessionize/Configuration/RegisterSessionize.cs` — how composers register `IHttpClientFactory` clients and options.
- `src/UmbracoCommunity.Web/Features/Sessionize/Infrastructure/SessionizeApiClient.cs` — the canonical pattern for "fetch JSON + cache + log" services. Your `CalendarFeedService` mirrors this shape closely. **One difference:** it must implement stale-on-error (Sessionize doesn't).
- `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/HeroBannerWithImageSlider.cshtml` — block partial conventions (`@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<BlockGridItem<TContent, TSettings>>`).
- `src/UmbracoCommunity.Web/Models/ContentModels/CardsBlock.cs` — block partial-class extension pattern (extends auto-generated `PublishedModels`).
- `Directory.Packages.props` — central package management; add new `PackageVersion` entries here, reference from csprojs without versions.

**Key project conventions:**
- Block partials in this project do NOT use `IViewModelBuilder` — they take `BlockGridItem<TContent, TSettings>` directly and call services via `@inject`.
- Models Builder runs in `SourceCodeManual` mode; you regenerate generated classes by running the site and using the backoffice "Generate models" button (Settings → Models Builder), or by saving any document type. Don't hand-edit `*.generated.cs` files.
- Main branch is `develop`. Work continues on the existing `feature/calendar-feed-block` branch.
- Build: `dotnet build` from repo root.

---

## File structure

**New files (Phase 1):**

```
src/UmbracoCommunity.Web/Features/Feeds/
├── Calendar/
│   ├── CalendarFeed.cs              ── DTOs (CalendarFeed, FeedMeta, CalendarEvent, AttendanceMode)
│   ├── ICalendarFeedService.cs
│   ├── CalendarFeedService.cs
│   ├── CalendarFeedHttpClient.cs    ── typed client marker
│   └── RoundRobinMerger.cs          ── static helper
└── Configuration/
    └── RegisterFeeds.cs             ── composer

src/UmbracoCommunity.Web/Models/ContentModels/
└── CalendarFeedBlock.cs             ── partial class extension (post-Models-Builder)

src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/
└── CalendarFeedBlock.cshtml

src/UmbracoCommunity.StaticAssets/src/css/blocks/
└── calendar-feed-block.css

tests/UmbracoCommunity.Web.Tests/
├── UmbracoCommunity.Web.Tests.csproj
├── Features/Feeds/Calendar/
│   ├── CalendarFeedJsonTests.cs
│   ├── CalendarFeedServiceTests.cs
│   └── RoundRobinMergerTests.cs
└── TestData/calendar-feed-sample.json
```

**Modified files:**
- `Directory.Packages.props` — add xUnit, Moq, FluentAssertions versions.
- `UmbracoCommunity.sln` — add Tests project.
- `src/UmbracoCommunity.StaticAssets/src/css/main.css` (or whichever entrypoint imports block CSS) — import `calendar-feed-block.css`.

---

## Task 1: Add the Tests project

**Files:**
- Create: `tests/UmbracoCommunity.Web.Tests/UmbracoCommunity.Web.Tests.csproj`
- Modify: `Directory.Packages.props`
- Modify: `UmbracoCommunity.sln`

- [ ] **Step 1: Add test package versions to central package management**

Open `Directory.Packages.props` and add the following inside the existing `<ItemGroup>` (alphabetical-ish, group with Microsoft entries is fine):

```xml
<!-- Testing -->
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
<PackageVersion Include="xunit" Version="2.9.2" />
<PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageVersion Include="Moq" Version="4.20.72" />
<PackageVersion Include="FluentAssertions" Version="6.12.1" />
<PackageVersion Include="Microsoft.Extensions.Caching.Memory" Version="10.0.7" />
<PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.7" />
<PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.7" />
```

- [ ] **Step 2: Create the Tests csproj**

Create `tests/UmbracoCommunity.Web.Tests/UmbracoCommunity.Web.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Moq" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.Extensions.Caching.Memory" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Http" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\UmbracoCommunity.Web\UmbracoCommunity.Web.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Add Tests project to the solution**

Run from the repo root:

```bash
dotnet sln UmbracoCommunity.sln add tests/UmbracoCommunity.Web.Tests/UmbracoCommunity.Web.Tests.csproj --solution-folder tests
```

Expected: a `tests` solution folder is created and the project is added.

- [ ] **Step 4: Verify the solution builds**

```bash
dotnet build UmbracoCommunity.sln
```

Expected: build succeeds with 0 errors. Warnings about empty test project are fine.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props tests/UmbracoCommunity.Web.Tests/ UmbracoCommunity.sln
git commit -m "Add UmbracoCommunity.Web.Tests xUnit project"
```

---

## Task 2: Define calendar feed DTOs

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeed.cs`
- Create: `tests/UmbracoCommunity.Web.Tests/TestData/calendar-feed-sample.json`
- Create: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/CalendarFeedJsonTests.cs`

This task defines the JSON-deserialisation target and proves the JSON shape from the spec round-trips into the typed model.

- [ ] **Step 1: Create the sample JSON fixture**

Create `tests/UmbracoCommunity.Web.Tests/TestData/calendar-feed-sample.json`:

```json
{
  "feed": {
    "title": "Umbraco events on Meetup.com",
    "sourceUrl": "https://umbracalendar.com/",
    "generatedAt": "2026-04-29T08:34:54Z"
  },
  "events": [
    {
      "id": "312837198",
      "url": "https://www.meetup.com/australian-umbraco-meetups/events/312837198/",
      "title": "Umbraco Melbourne Meetup",
      "summary": "Some notes here",
      "publishedAt": "2026-01-11T22:30:51Z",
      "startsAt": "2026-07-16T18:00:00+10:00",
      "endsAt": "2026-07-16T19:30:00+10:00",
      "location": "Luminary, 195 Little Collins Street, Melbourne, AU",
      "organizer": "Umbraco Melbourne Meetup",
      "attendanceMode": "inPerson",
      "isHqOrganized": false,
      "isCancelled": false
    },
    {
      "id": "9d2dd239-ff63-468b-9096-6a2172c3c128",
      "url": "https://codegarden.umbraco.com/",
      "title": "Codegarden",
      "summary": null,
      "publishedAt": "2026-02-16T13:59:43Z",
      "startsAt": "2026-06-10T08:00:00+00:00",
      "endsAt": "2026-06-11T22:00:00+00:00",
      "location": null,
      "organizer": "Umbraco Community",
      "attendanceMode": "hybrid",
      "isHqOrganized": true,
      "isCancelled": false
    },
    {
      "id": "online-1",
      "url": "https://example.com/online-event",
      "title": "Online community call",
      "summary": null,
      "publishedAt": "2026-03-01T00:00:00Z",
      "startsAt": "2026-05-01T17:00:00+00:00",
      "endsAt": "2026-05-01T18:00:00+00:00",
      "location": null,
      "organizer": null,
      "attendanceMode": "online",
      "isHqOrganized": false,
      "isCancelled": true
    }
  ]
}
```

Mark the file as copied to the test output directory by adding to the test csproj inside `<Project>` (above the closing tag):

```xml
<ItemGroup>
  <None Update="TestData\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 2: Write the failing deserialisation test**

Create `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/CalendarFeedJsonTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using UmbracoCommunity.Web.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

public class CalendarFeedJsonTests
{
    private static CalendarFeed Deserialise(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        var json = File.ReadAllText(path);
        var result = JsonSerializer.Deserialize<CalendarFeed>(json, CalendarFeedJsonOptions.Default);
        result.Should().NotBeNull();
        return result!;
    }

    [Fact]
    public void Deserialises_feed_metadata()
    {
        var feed = Deserialise("calendar-feed-sample.json");

        feed.Feed.Title.Should().Be("Umbraco events on Meetup.com");
        feed.Feed.SourceUrl.Should().Be("https://umbracalendar.com/");
        feed.Feed.GeneratedAt.Should().Be(DateTimeOffset.Parse("2026-04-29T08:34:54Z"));
    }

    [Fact]
    public void Deserialises_three_events()
    {
        var feed = Deserialise("calendar-feed-sample.json");
        feed.Events.Should().HaveCount(3);
    }

    [Fact]
    public void Maps_event_fields_correctly()
    {
        var feed = Deserialise("calendar-feed-sample.json");
        var melbourne = feed.Events[0];

        melbourne.Id.Should().Be("312837198");
        melbourne.Url.Should().Be("https://www.meetup.com/australian-umbraco-meetups/events/312837198/");
        melbourne.Title.Should().Be("Umbraco Melbourne Meetup");
        melbourne.Summary.Should().Be("Some notes here");
        melbourne.StartsAt.Should().Be(DateTimeOffset.Parse("2026-07-16T18:00:00+10:00"));
        melbourne.EndsAt.Should().Be(DateTimeOffset.Parse("2026-07-16T19:30:00+10:00"));
        melbourne.Location.Should().Be("Luminary, 195 Little Collins Street, Melbourne, AU");
        melbourne.Organizer.Should().Be("Umbraco Melbourne Meetup");
        melbourne.AttendanceMode.Should().Be(AttendanceMode.InPerson);
        melbourne.IsHqOrganized.Should().BeFalse();
        melbourne.IsCancelled.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, AttendanceMode.InPerson)]
    [InlineData(1, AttendanceMode.Hybrid)]
    [InlineData(2, AttendanceMode.Online)]
    public void Maps_all_attendance_modes(int index, AttendanceMode expected)
    {
        var feed = Deserialise("calendar-feed-sample.json");
        feed.Events[index].AttendanceMode.Should().Be(expected);
    }

    [Fact]
    public void Allows_null_summary_location_and_organizer()
    {
        var feed = Deserialise("calendar-feed-sample.json");
        var codegarden = feed.Events[1];

        codegarden.Summary.Should().BeNull();
        codegarden.Location.Should().BeNull();
    }

    [Fact]
    public void Maps_isCancelled_and_isHqOrganized()
    {
        var feed = Deserialise("calendar-feed-sample.json");

        feed.Events[1].IsHqOrganized.Should().BeTrue();
        feed.Events[2].IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void Unknown_attendance_mode_falls_back_to_unknown()
    {
        const string json = """
        {
          "feed": { "title": "x", "sourceUrl": "https://x", "generatedAt": "2026-01-01T00:00:00Z" },
          "events": [{
            "id": "1", "url": "https://x", "title": "x", "summary": null,
            "publishedAt": "2026-01-01T00:00:00Z",
            "startsAt": "2026-01-01T00:00:00Z", "endsAt": "2026-01-01T01:00:00Z",
            "location": null, "organizer": null,
            "attendanceMode": "futureValueNotInEnum",
            "isHqOrganized": false, "isCancelled": false
          }]
        }
        """;
        var feed = JsonSerializer.Deserialize<CalendarFeed>(json, CalendarFeedJsonOptions.Default)!;
        feed.Events[0].AttendanceMode.Should().Be(AttendanceMode.Unknown);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/UmbracoCommunity.Web.Tests/ --filter FullyQualifiedName~CalendarFeedJsonTests
```

Expected: compile errors — `CalendarFeed`, `CalendarEvent`, `AttendanceMode`, `CalendarFeedJsonOptions` don't exist yet.

- [ ] **Step 4: Implement the DTOs and JSON options**

Create `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeed.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public sealed record CalendarFeed(
    FeedMeta Feed,
    IReadOnlyList<CalendarEvent> Events);

public sealed record FeedMeta(
    string Title,
    string SourceUrl,
    DateTimeOffset GeneratedAt);

public sealed record CalendarEvent(
    string Id,
    string Url,
    string Title,
    string? Summary,
    DateTimeOffset PublishedAt,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Location,
    string? Organizer,
    AttendanceMode AttendanceMode,
    bool IsHqOrganized,
    bool IsCancelled);

public enum AttendanceMode
{
    Unknown = 0,
    InPerson,
    Online,
    Hybrid,
}

public static class CalendarFeedJsonOptions
{
    public static readonly JsonSerializerOptions Default = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new AttendanceModeConverter());
        return options;
    }

    private sealed class AttendanceModeConverter : JsonConverter<AttendanceMode>
    {
        public override AttendanceMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return value switch
            {
                "inPerson" => AttendanceMode.InPerson,
                "online" => AttendanceMode.Online,
                "hybrid" => AttendanceMode.Hybrid,
                _ => AttendanceMode.Unknown,
            };
        }

        public override void Write(Utf8JsonWriter writer, AttendanceMode value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value switch
            {
                AttendanceMode.InPerson => "inPerson",
                AttendanceMode.Online => "online",
                AttendanceMode.Hybrid => "hybrid",
                _ => "unknown",
            });
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/UmbracoCommunity.Web.Tests/ --filter FullyQualifiedName~CalendarFeedJsonTests
```

Expected: all 7 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/ tests/UmbracoCommunity.Web.Tests/
git commit -m "Add calendar feed DTOs with JSON deserialisation tests"
```

---

## Task 3: Round-robin merger

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/Calendar/RoundRobinMerger.cs`
- Create: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/RoundRobinMergerTests.cs`

Used by the block partial to combine multiple per-feed event lists into a single `maxItems`-bounded list while preserving each feed's per-feed sort order.

- [ ] **Step 1: Write failing tests**

Create `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/RoundRobinMergerTests.cs`:

```csharp
using FluentAssertions;
using UmbracoCommunity.Web.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

public class RoundRobinMergerTests
{
    [Fact]
    public void Single_feed_returns_items_in_order_capped_at_max()
    {
        var feed = new[] { "a1", "a2", "a3", "a4" };
        var result = RoundRobinMerger.Merge(new[] { feed }, maxItems: 3);
        result.Should().Equal("a1", "a2", "a3");
    }

    [Fact]
    public void Two_feeds_interleave_round_robin()
    {
        var a = new[] { "a1", "a2", "a3" };
        var b = new[] { "b1", "b2", "b3" };
        var result = RoundRobinMerger.Merge(new[] { a, b }, maxItems: 6);
        result.Should().Equal("a1", "b1", "a2", "b2", "a3", "b3");
    }

    [Fact]
    public void Exhausted_feed_is_skipped()
    {
        var a = new[] { "a1" };
        var b = new[] { "b1", "b2", "b3" };
        var result = RoundRobinMerger.Merge(new[] { a, b }, maxItems: 4);
        result.Should().Equal("a1", "b1", "b2", "b3");
    }

    [Fact]
    public void Stops_at_max_items_even_if_more_available()
    {
        var a = new[] { "a1", "a2", "a3" };
        var b = new[] { "b1", "b2", "b3" };
        var result = RoundRobinMerger.Merge(new[] { a, b }, maxItems: 3);
        result.Should().Equal("a1", "b1", "a2");
    }

    [Fact]
    public void Empty_feeds_collection_returns_empty()
    {
        var result = RoundRobinMerger.Merge(Array.Empty<string[]>(), maxItems: 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public void All_feeds_empty_returns_empty()
    {
        var result = RoundRobinMerger.Merge(new[] { Array.Empty<string>(), Array.Empty<string>() }, maxItems: 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Zero_max_items_returns_empty()
    {
        var a = new[] { "a1", "a2" };
        var result = RoundRobinMerger.Merge(new[] { a }, maxItems: 0);
        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/UmbracoCommunity.Web.Tests/ --filter FullyQualifiedName~RoundRobinMergerTests
```

Expected: compile error — `RoundRobinMerger` does not exist.

- [ ] **Step 3: Implement RoundRobinMerger**

Create `src/UmbracoCommunity.Web/Features/Feeds/Calendar/RoundRobinMerger.cs`:

```csharp
namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public static class RoundRobinMerger
{
    public static IReadOnlyList<T> Merge<T>(IReadOnlyList<IReadOnlyList<T>> sources, int maxItems)
    {
        if (maxItems <= 0 || sources.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>(maxItems);
        var indices = new int[sources.Count];
        var anyTaken = true;

        while (result.Count < maxItems && anyTaken)
        {
            anyTaken = false;
            for (var s = 0; s < sources.Count && result.Count < maxItems; s++)
            {
                var source = sources[s];
                var i = indices[s];
                if (i < source.Count)
                {
                    result.Add(source[i]);
                    indices[s] = i + 1;
                    anyTaken = true;
                }
            }
        }

        return result;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/UmbracoCommunity.Web.Tests/ --filter FullyQualifiedName~RoundRobinMergerTests
```

Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/Calendar/RoundRobinMerger.cs tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/RoundRobinMergerTests.cs
git commit -m "Add RoundRobinMerger for combining per-feed event lists"
```

---

## Task 4: ICalendarFeedService interface and HTTP client marker

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/Calendar/ICalendarFeedService.cs`
- Create: `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedHttpClient.cs`

These are tiny scaffolding files; no tests needed for the interface itself — service tests in Task 5 cover the implementation.

- [ ] **Step 1: Create the interface**

Create `src/UmbracoCommunity.Web/Features/Feeds/Calendar/ICalendarFeedService.cs`:

```csharp
using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public interface ICalendarFeedService
{
    /// <summary>
    /// Fetches the feed for the given calendarFeed published content node, filters out past events,
    /// sorts ascending by StartsAt, and returns the result. Caches per node for the configured duration.
    /// On error, returns the last successful response from a 7-day stale fallback if available, else empty.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(
        IPublishedContent feedNode,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create the typed HttpClient marker**

Create `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedHttpClient.cs`:

```csharp
namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

/// <summary>
/// Marker type for the named HttpClient used by <see cref="CalendarFeedService"/>.
/// Configured in <c>RegisterFeeds</c>.
/// </summary>
public sealed class CalendarFeedHttpClient
{
    public HttpClient Client { get; }

    public CalendarFeedHttpClient(HttpClient client) => Client = client;
}
```

- [ ] **Step 3: Build to verify it compiles**

```bash
dotnet build src/UmbracoCommunity.Web/
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/Calendar/ICalendarFeedService.cs src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedHttpClient.cs
git commit -m "Add ICalendarFeedService and HttpClient marker"
```

---

## Task 5: CalendarFeedService implementation

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedService.cs`
- Create: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/CalendarFeedServiceTests.cs`
- Create: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/StubPublishedContent.cs`
- Create: `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/StubHandler.cs`

The bulk of the backend work. Implements: fetch JSON via HttpClient, deserialise, filter past events, sort, cache, stale-on-error.

- [ ] **Step 1: Add stub helpers**

Calendar feed nodes will provide three properties (`feedUrl`, `cacheDurationMinutes`) the service reads from `IPublishedContent`. Tests can't construct a real `IPublishedContent`; provide a minimal stub.

Create `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/StubPublishedContent.cs`:

```csharp
using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

/// <summary>
/// Minimal IPublishedContent stub. Only Key, Name, and the calendar feed properties (feedUrl,
/// cacheDurationMinutes) are populated. Anything else throws — tests will surface unintended
/// dependencies on Umbraco internals.
/// </summary>
internal sealed class StubPublishedContent : IPublishedContent
{
    public required Guid Key { get; init; }
    public required string FeedUrl { get; init; }
    public required int CacheDurationMinutes { get; init; }

    public string Name => "Stub feed";

    public T Value<T>(string alias, string? culture = null, string? segment = null,
        Fallback fallback = default, T? defaultValue = default)
    {
        if (alias == "feedUrl" && typeof(T) == typeof(string))
            return (T)(object)FeedUrl;
        if (alias == "cacheDurationMinutes" && typeof(T) == typeof(int))
            return (T)(object)CacheDurationMinutes;
        return defaultValue!;
    }

    public object? Value(string alias, string? culture = null, string? segment = null,
        Fallback fallback = default, object? defaultValue = null)
        => Value<object?>(alias, culture, segment, fallback, defaultValue);

    // The remainder of IPublishedContent is unused by CalendarFeedService.
    public int Id => throw new NotSupportedException();
    public string UrlSegment => throw new NotSupportedException();
    public int SortOrder => 0;
    public int Level => 0;
    public string Path => string.Empty;
    public int? TemplateId => null;
    public int CreatorId => 0;
    public DateTime CreateDate => default;
    public int WriterId => 0;
    public DateTime UpdateDate => default;
    public IReadOnlyDictionary<string, PublishedCultureInfo> Cultures => new Dictionary<string, PublishedCultureInfo>();
    public PublishedItemType ItemType => PublishedItemType.Content;
    public bool IsDraft(string? culture = null) => false;
    public bool IsPublished(string? culture = null) => true;
    public IPublishedContent? Parent => null;
    public IEnumerable<IPublishedContent> Children => Array.Empty<IPublishedContent>();
    public IEnumerable<IPublishedContent> ChildrenForAllCultures => Array.Empty<IPublishedContent>();
    public IPublishedContentType ContentType => throw new NotSupportedException();
    public IEnumerable<IPublishedProperty> Properties => Array.Empty<IPublishedProperty>();
    public IPublishedProperty? GetProperty(string alias) => null;
}
```

- [ ] **Step 2: Add a stub HttpMessageHandler**

Create `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/StubHandler.cs`:

```csharp
using System.Net;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public int CallCount { get; private set; }

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public static StubHandler Json(string body) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    });

    public static StubHandler Throws() => new(_ => throw new HttpRequestException("simulated network error"));

    public static StubHandler Status(HttpStatusCode code) => new(_ => new HttpResponseMessage(code));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(_responder(request));
    }
}
```

- [ ] **Step 3: Write failing service tests**

Create `tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/CalendarFeedServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;

public class CalendarFeedServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-29T10:00:00Z");

    private static string SampleJson(params (string id, string startsAt, string endsAt)[] events)
    {
        var items = string.Join(",", events.Select(e => $$"""
        {
          "id": "{{e.id}}",
          "url": "https://x/{{e.id}}",
          "title": "Event {{e.id}}",
          "summary": null,
          "publishedAt": "2026-01-01T00:00:00Z",
          "startsAt": "{{e.startsAt}}",
          "endsAt": "{{e.endsAt}}",
          "location": null,
          "organizer": null,
          "attendanceMode": "inPerson",
          "isHqOrganized": false,
          "isCancelled": false
        }
        """));
        return $$"""
        {
          "feed": { "title": "x", "sourceUrl": "https://x", "generatedAt": "2026-04-29T08:00:00Z" },
          "events": [{{items}}]
        }
        """;
    }

    private static (CalendarFeedService Service, StubHandler Handler, MemoryCache Cache) CreateService(
        StubHandler handler,
        TimeProvider? timeProvider = null)
    {
        var http = new HttpClient(handler);
        var typed = new CalendarFeedHttpClient(http);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<CalendarFeedService>.Instance;
        var service = new CalendarFeedService(typed, cache, logger, timeProvider ?? new FixedTimeProvider(Now));
        return (service, handler, cache);
    }

    private static StubPublishedContent FeedNode(string url = "https://example.com/feed.json", int cacheMinutes = 60)
        => new() { Key = Guid.NewGuid(), FeedUrl = url, CacheDurationMinutes = cacheMinutes };

    [Fact]
    public async Task Returns_events_sorted_ascending_by_startsAt()
    {
        var json = SampleJson(
            ("c", "2026-08-01T00:00:00Z", "2026-08-01T01:00:00Z"),
            ("a", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"),
            ("b", "2026-06-01T00:00:00Z", "2026-06-01T01:00:00Z"));

        var (service, _, _) = CreateService(StubHandler.Json(json));
        var result = await service.GetUpcomingEventsAsync(FeedNode());

        result.Select(e => e.Id).Should().Equal("a", "b", "c");
    }

    [Fact]
    public async Task Filters_out_events_that_have_already_ended()
    {
        var json = SampleJson(
            ("past",   "2026-04-29T08:00:00Z", "2026-04-29T09:59:59Z"), // ended before Now (10:00)
            ("future", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"));

        var (service, _, _) = CreateService(StubHandler.Json(json));
        var result = await service.GetUpcomingEventsAsync(FeedNode());

        result.Select(e => e.Id).Should().Equal("future");
    }

    [Fact]
    public async Task Event_ending_exactly_now_is_filtered()
    {
        var json = SampleJson(
            ("ending-now", "2026-04-29T08:00:00Z", "2026-04-29T10:00:00Z"));

        var (service, _, _) = CreateService(StubHandler.Json(json));
        var result = await service.GetUpcomingEventsAsync(FeedNode());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Caches_response_for_configured_duration()
    {
        var json = SampleJson(("a", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"));
        var (service, handler, _) = CreateService(StubHandler.Json(json));
        var node = FeedNode(cacheMinutes: 60);

        await service.GetUpcomingEventsAsync(node);
        await service.GetUpcomingEventsAsync(node);

        handler.CallCount.Should().Be(1, "second request should be served from cache");
    }

    [Fact]
    public async Task Different_feed_nodes_have_separate_cache_entries()
    {
        var json = SampleJson(("a", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"));
        var (service, handler, _) = CreateService(StubHandler.Json(json));

        await service.GetUpcomingEventsAsync(FeedNode());
        await service.GetUpcomingEventsAsync(FeedNode()); // different Key

        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Returns_empty_on_first_failure_with_no_stale_fallback()
    {
        var (service, _, _) = CreateService(StubHandler.Throws());
        var result = await service.GetUpcomingEventsAsync(FeedNode());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_stale_fallback_when_upstream_fails_after_a_success()
    {
        var goodJson = SampleJson(("good", "2026-05-01T00:00:00Z", "2026-05-01T01:00:00Z"));
        var node = FeedNode(cacheMinutes: 1);

        var responses = new Queue<Func<HttpResponseMessage>>();
        responses.Enqueue(() => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(goodJson, System.Text.Encoding.UTF8, "application/json") });
        responses.Enqueue(() => throw new HttpRequestException("down"));

        var handler = new StubHandler(_ => responses.Dequeue()());
        var time = new FixedTimeProvider(Now);
        var (service, _, _) = CreateService(handler, time);

        // First call: success, populates both primary and stale caches.
        var first = await service.GetUpcomingEventsAsync(node);
        first.Select(e => e.Id).Should().Equal("good");

        // Advance past primary cache expiry (1 minute) so next call hits HTTP.
        time.Advance(TimeSpan.FromMinutes(2));

        // Second call: HTTP throws, but stale fallback still has the prior result.
        var second = await service.GetUpcomingEventsAsync(node);
        second.Select(e => e.Id).Should().Equal("good");
    }

    [Fact]
    public async Task Returns_empty_when_status_code_is_non_2xx_and_no_fallback()
    {
        var (service, _, _) = CreateService(StubHandler.Status(System.Net.HttpStatusCode.InternalServerError));
        var result = await service.GetUpcomingEventsAsync(FeedNode());
        result.Should().BeEmpty();
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public FixedTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
dotnet test tests/UmbracoCommunity.Web.Tests/ --filter FullyQualifiedName~CalendarFeedServiceTests
```

Expected: compile error — `CalendarFeedService` does not exist.

- [ ] **Step 5: Implement CalendarFeedService**

Create `src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedService.cs`:

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public sealed class CalendarFeedService : ICalendarFeedService
{
    private static readonly TimeSpan StaleFallbackDuration = TimeSpan.FromDays(7);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CalendarFeedService> _logger;
    private readonly TimeProvider _time;

    public CalendarFeedService(
        CalendarFeedHttpClient typedClient,
        IMemoryCache cache,
        ILogger<CalendarFeedService> logger,
        TimeProvider time)
    {
        _http = typedClient.Client;
        _cache = cache;
        _logger = logger;
        _time = time;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(
        IPublishedContent feedNode,
        CancellationToken cancellationToken = default)
    {
        var feedUrl = feedNode.Value<string>("feedUrl");
        var cacheMinutes = feedNode.Value<int>("cacheDurationMinutes");
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            _logger.LogWarning("Calendar feed node {Key} has no feedUrl; returning empty.", feedNode.Key);
            return Array.Empty<CalendarEvent>();
        }

        var primaryKey = $"calendar-feed:{feedNode.Key}";
        var staleKey = $"calendar-feed:{feedNode.Key}:stale";

        if (_cache.TryGetValue(primaryKey, out IReadOnlyList<CalendarEvent>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var feed = await _http.GetFromJsonAsync<CalendarFeed>(
                feedUrl, CalendarFeedJsonOptions.Default, cancellationToken)
                ?? throw new InvalidOperationException("Feed deserialised to null.");

            var upcoming = ProjectAndSort(feed);

            var primaryDuration = TimeSpan.FromMinutes(Math.Max(1, cacheMinutes));
            _cache.Set(primaryKey, upcoming, primaryDuration);
            _cache.Set(staleKey, upcoming, new MemoryCacheEntryOptions
            {
                SlidingExpiration = StaleFallbackDuration,
            });

            return upcoming;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to fetch calendar feed for node {Key} ({Url}); attempting stale fallback.",
                feedNode.Key, feedUrl);

            if (_cache.TryGetValue(staleKey, out IReadOnlyList<CalendarEvent>? stale) && stale is not null)
            {
                return stale;
            }

            return Array.Empty<CalendarEvent>();
        }
    }

    private IReadOnlyList<CalendarEvent> ProjectAndSort(CalendarFeed feed)
    {
        var nowUtc = _time.GetUtcNow();
        return feed.Events
            .Where(e => e.EndsAt > nowUtc)
            .OrderBy(e => e.StartsAt)
            .ToArray();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/UmbracoCommunity.Web.Tests/
```

Expected: all 22 tests (Tasks 2 + 3 + 5) pass.

- [ ] **Step 7: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/Calendar/CalendarFeedService.cs tests/UmbracoCommunity.Web.Tests/Features/Feeds/Calendar/
git commit -m "Implement CalendarFeedService with caching and stale-on-error fallback"
```

---

## Task 6: Composer to register feed services

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs`

- [ ] **Step 1: Create the composer**

Create `src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using UmbracoCommunity.Web.Features.Feeds.Calendar;

namespace UmbracoCommunity.Web.Features.Feeds.Configuration;

public sealed class RegisterFeeds : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddHttpClient<CalendarFeedHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.Services.AddSingleton<ICalendarFeedService, CalendarFeedService>();
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build src/UmbracoCommunity.Web/
```

Expected: build succeeds. Composers are auto-discovered by Umbraco at startup; no other registration is needed.

- [ ] **Step 3: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs
git commit -m "Register calendar feed service and HttpClient"
```

---

## Task 7: Backoffice document type setup (manual)

This task is performed in the Umbraco backoffice UI by a human. The implementer should follow these steps exactly and verify each, then **regenerate Models Builder classes** in Step 7.

**No commits during the manual steps** — commit after Models Builder regeneration in Task 8.

- [ ] **Step 1: Start the site**

```bash
cd src/UmbracoCommunity.Web.UI
dotnet run
```

In a second terminal:
```bash
cd src/UmbracoCommunity.StaticAssets
npm run dev
```

Log into the backoffice (`/umbraco`) with `community@umbraco.com` / `community!`.

- [ ] **Step 2: Create the `feedsContainer` document type**

Settings → Document Types → right-click root → "Create folder" or new doc type:
- **Alias:** `feedsContainer`
- **Name:** Feeds container
- **Icon:** `icon-rss color-blue`
- **Allow as root:** No
- **Allow vary by culture:** No
- **No tabs/properties** — pure container.

- [ ] **Step 3: Create the `calendarFeed` document type**

- **Alias:** `calendarFeed`
- **Name:** Calendar Feed
- **Icon:** `icon-calendar color-blue`
- **Allow as root:** No
- Add properties (single tab "Settings"):
  | Alias | Name | Editor | Mandatory | Validation/notes |
  |---|---|---|---|---|
  | `feedUrl` | Feed URL | Textstring | Yes | Help text: "Full URL of the JSON feed (e.g. https://umbracalendar.com/meetup.json)" |
  | `cacheDurationMinutes` | Cache duration (minutes) | Numeric | Yes | Default value: 60. Min: 1. |

- - [ ] Save.

- [ ] **Step 4: Allow `calendarFeed` under `feedsContainer`**

Edit `feedsContainer` → Permissions → Allowed child node types → add `calendarFeed`. Save.

- [ ] **Step 5: Allow `feedsContainer` under the Settings doc type**

Edit the existing `siteSettings` (or whatever the per-tenant Settings document type is named — check the existing tree under any tenant root, the child labelled "Settings" — its alias is in the tab title). Permissions → Allowed child node types → add `feedsContainer`. Save.

- [ ] **Step 6: Create the `calendarFeedBlock` element type**

Document Types → new element type:
- **Alias:** `calendarFeedBlock`
- **Name:** Calendar Feed Block
- **Icon:** `icon-calendar-alt color-blue`
- **Is element type:** Yes
- Properties (single tab "Content"):
  | Alias | Name | Editor | Mandatory | Notes |
  |---|---|---|---|---|
  | `headline` | Headline | Textstring | No | |
  | `feeds` | Feeds | Multinode Treepicker | Yes | Min 1, max 10. Filter by document type: `calendarFeed`. Dynamic root: closest ancestor implementing the per-tenant settings root (mirror the Authors picker config — open the existing Author picker on `article` doc type, copy the dynamic-root setup). Path step: descendant of `feedsContainer`. |
  | `maxItems` | Max items | Numeric | Yes | Default: 5. Min: 1. Max: 50. |

- [ ] **Step 7: Create the `settingsCalendarFeedBlock` settings element type**

Mirror the smallest existing settings type (e.g. `settingsTitleBlock`) for spacing/background:
- **Alias:** `settingsCalendarFeedBlock`
- **Name:** Settings: Calendar Feed Block
- **Is element type:** Yes
- Compose with the same compositions used by `settingsTitleBlock` (open it and copy compositions exactly — these provide background colour and padding).

- [ ] **Step 8: Add the block to the block grid datatype on Article and Page**

Find the block grid data type used on the homepage (`Models/ContentModels/CardsBlock.cs` referenced via the article's `contentBlocks` — open the data type, see which one). Add `calendarFeedBlock` as an allowed block, with `settingsCalendarFeedBlock` as its settings. Place it in a sensible group (e.g. "Content" or whichever group meetup-style blocks live in).

- [ ] **Step 9: Verify in the content tree**

Navigate to a tenant's Settings node. Confirm you can:
1. Add a "Feeds container" child.
2. Inside that, add a "Calendar Feed" child with URL `https://umbracalendar.com/meetup.json` (or a placeholder URL — actual umbracalendar JSON endpoint may not exist yet; use a local fixture file via `dotnet run`-served path or placeholder while developing the view).
3. Publish both.
4. On a content page, edit the block grid and find "Calendar Feed Block" available. Don't add it yet — that's Task 9 verification.

---

## Task 8: Regenerate Models Builder classes and add partial extension

**Files:**
- Auto-generated: `src/UmbracoCommunity.Web/Models/PublishedModels/CalendarFeedBlock.generated.cs`, `SettingsCalendarFeedBlock.generated.cs`, `CalendarFeed.generated.cs`, `FeedsContainer.generated.cs`
- Create: `src/UmbracoCommunity.Web/Models/ContentModels/CalendarFeedBlock.cs`

- [ ] **Step 1: Trigger Models Builder regeneration**

In the backoffice: Settings → Models Builder → click "Generate models" (or save any document type, which also triggers it). Verify the new files appear:

```bash
ls src/UmbracoCommunity.Web/Models/PublishedModels/ | grep -E "CalendarFeed|FeedsContainer|SettingsCalendarFeedBlock"
```

Expected (4 files):
```
CalendarFeed.generated.cs
CalendarFeedBlock.generated.cs
FeedsContainer.generated.cs
SettingsCalendarFeedBlock.generated.cs
```

- [ ] **Step 2: Add a partial-class extension for the block**

Mirror the pattern in `Models/ContentModels/CardsBlock.cs`. Create `src/UmbracoCommunity.Web/Models/ContentModels/CalendarFeedBlock.cs`:

```csharp
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class CalendarFeedBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
```

(`IdHash` is used by other blocks for unique DOM element IDs in their partials. We'll use it in Task 9.)

- [ ] **Step 3: Build to verify**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/UmbracoCommunity.Web/Models/PublishedModels/CalendarFeed.generated.cs \
        src/UmbracoCommunity.Web/Models/PublishedModels/CalendarFeedBlock.generated.cs \
        src/UmbracoCommunity.Web/Models/PublishedModels/FeedsContainer.generated.cs \
        src/UmbracoCommunity.Web/Models/PublishedModels/SettingsCalendarFeedBlock.generated.cs \
        src/UmbracoCommunity.Web/Models/ContentModels/CalendarFeedBlock.cs
git commit -m "Add Models Builder classes and partial extension for calendar feed block"
```

---

## Task 9: Block Razor partial

**Files:**
- Create: `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CalendarFeedBlock.cshtml`

- [ ] **Step 1: Create the partial**

Create `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CalendarFeedBlock.cshtml`:

```cshtml
@using System.Globalization
@using Umbraco.Cms.Core.Models.Blocks
@using Umbraco.Cms.Core.Models.PublishedContent
@using UmbracoCommunity.Web.Features.Feeds.Calendar
@using UmbracoCommunity.Web.Models.PublishedModels
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<BlockGridItem<CalendarFeedBlock, SettingsCalendarFeedBlock>>
@inject ICalendarFeedService FeedService

@{
    var feeds = (Model.Content.Feeds ?? Enumerable.Empty<IPublishedContent>()).ToList();
    var maxItems = Math.Max(1, Model.Content.MaxItems);

    var perFeed = await Task.WhenAll(feeds.Select(f => FeedService.GetUpcomingEventsAsync(f)));

    var events = RoundRobinMerger.Merge(perFeed, maxItems);
}

@if (events.Count == 0 && string.IsNullOrWhiteSpace(Model.Content.Headline))
{
    return;
}

<section class="dc-calendar-feed dc-calendar-feed--@Model.Content.IdHash">
    @if (!string.IsNullOrWhiteSpace(Model.Content.Headline))
    {
        <h2 class="dc-calendar-feed__headline">@Model.Content.Headline</h2>
    }

    @if (events.Count > 0)
    {
        <ul class="dc-calendar-feed__list">
            @foreach (var ev in events)
            {
                var classes = "dc-calendar-feed__item";
                if (ev.IsCancelled) { classes += " is-cancelled"; }

                <li class="@classes">
                    <a class="dc-calendar-feed__title" href="@ev.Url" rel="noopener" target="_blank">
                        @ev.Title
                    </a>
                    <div class="dc-calendar-feed__meta">
                        <time datetime="@ev.StartsAt.ToString("o", CultureInfo.InvariantCulture)">
                            @ev.StartsAt.ToString("ddd d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
                        </time>
                        @if (!string.IsNullOrWhiteSpace(ev.Location))
                        {
                            <span class="dc-calendar-feed__location">· @ev.Location</span>
                        }
                        @if (!string.IsNullOrWhiteSpace(ev.Organizer))
                        {
                            <span class="dc-calendar-feed__organizer">· @ev.Organizer</span>
                        }
                    </div>
                    <div class="dc-calendar-feed__badges">
                        @if (ev.IsHqOrganized)
                        {
                            <span class="dc-calendar-feed__badge dc-calendar-feed__badge--hq">HQ-organized</span>
                        }
                        @if (ev.IsCancelled)
                        {
                            <span class="dc-calendar-feed__badge dc-calendar-feed__badge--cancelled">Cancelled</span>
                        }
                        <span class="dc-calendar-feed__badge dc-calendar-feed__badge--mode">@ev.AttendanceMode</span>
                    </div>
                </li>
            }
        </ul>
    }
</section>
```

- [ ] **Step 2: Verify the file compiles by building**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CalendarFeedBlock.cshtml
git commit -m "Add CalendarFeedBlock Razor partial"
```

---

## Task 10: Block CSS

**Files:**
- Create: `src/UmbracoCommunity.StaticAssets/src/css/blocks/calendar-feed-block.css`
- Modify: whichever entrypoint imports block CSS (find with `grep -r "@import" src/UmbracoCommunity.StaticAssets/src/css/blocks/`)

This is intentionally a starting-point stylesheet; visual polish lives in a separate design pass.

- [ ] **Step 1: Locate the block CSS index**

```bash
grep -rE "blocks/.*\.css" src/UmbracoCommunity.StaticAssets/src/css/ | head
```

Open whichever file imports peer files like `slider-block.css` and add an import for `calendar-feed-block.css` alongside.

- [ ] **Step 2: Create the stylesheet**

Create `src/UmbracoCommunity.StaticAssets/src/css/blocks/calendar-feed-block.css`:

```css
.dc-calendar-feed {
    display: flex;
    flex-direction: column;
    gap: var(--space-md);
}

.dc-calendar-feed__headline {
    margin: 0;
}

.dc-calendar-feed__list {
    list-style: none;
    margin: 0;
    padding: 0;
    display: grid;
    gap: var(--space-sm);
}

.dc-calendar-feed__item {
    display: flex;
    flex-direction: column;
    gap: var(--space-xxs);
    padding: var(--space-sm);
    border: 1px solid var(--color-border, currentColor);
    border-radius: var(--radius-sm, 4px);
}

.dc-calendar-feed__item.is-cancelled .dc-calendar-feed__title {
    text-decoration: line-through;
    opacity: 0.7;
}

.dc-calendar-feed__title {
    font-weight: 600;
    font-size: 1.125rem;
}

.dc-calendar-feed__meta {
    font-size: 0.9rem;
    opacity: 0.85;
}

.dc-calendar-feed__badges {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-xxs);
}

.dc-calendar-feed__badge {
    display: inline-block;
    padding: 0.125rem 0.5rem;
    border-radius: 999px;
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    background: var(--color-badge-bg, currentColor);
    color: var(--color-badge-fg, #fff);
}

.dc-calendar-feed__badge--hq {
    --color-badge-bg: var(--color-blue, #3544b1);
}

.dc-calendar-feed__badge--cancelled {
    --color-badge-bg: var(--color-red, #c83434);
}
```

(Existing rhythm-mixin variables like `--space-md` are available globally; if any of these custom-property names don't match the project's conventions, reuse the names that other block CSS files use — grep to confirm.)

- [ ] **Step 3: Add the import to the block index**

In whichever CSS file imports the other block stylesheets, add:

```css
@import "./blocks/calendar-feed-block.css";
```

(Match the path style of neighbouring imports.)

- [ ] **Step 4: Run the frontend build to verify**

```bash
cd src/UmbracoCommunity.StaticAssets
npm run build
```

Expected: build succeeds; `dist/` contains the new styles.

- [ ] **Step 5: Commit**

```bash
git add src/UmbracoCommunity.StaticAssets/src/css/
git commit -m "Add styles for calendar feed block"
```

---

## Task 11: End-to-end manual verification

The C# tests cover the service layer; the rest needs human-in-the-loop verification.

- [ ] **Step 1: Start both servers**

```bash
# Terminal 1
cd src/UmbracoCommunity.Web.UI && dotnet run
```

```bash
# Terminal 2
cd src/UmbracoCommunity.StaticAssets && npm run dev
```

- [ ] **Step 2: Configure a test feed**

If the umbracalendar JSON endpoint is live, point the feed URL at it. Otherwise, host the sample fixture locally:
- Copy `tests/UmbracoCommunity.Web.Tests/TestData/calendar-feed-sample.json` to `src/UmbracoCommunity.Web.UI/wwwroot/test-feeds/calendar-feed-sample.json`.
- Use feed URL `https://localhost:<port>/test-feeds/calendar-feed-sample.json` on the `calendarFeed` node.
- **Remove** the `wwwroot/test-feeds/` folder before merging.

- [ ] **Step 3: Add the block to the homepage**

In the backoffice, edit the homepage of any tenant. Add a "Calendar Feed Block" to the block grid. Pick the configured feed. Set Max items = 5. Save and publish.

- [ ] **Step 4: Verify on the front end**

Visit the homepage. Expect:
- The headline (if set) appears.
- Up to 5 upcoming events appear, sorted by start date ascending.
- Past events are absent.
- A cancelled event renders with the `is-cancelled` class and a red "Cancelled" badge.
- An HQ-organized event renders with a blue "HQ-organized" badge.
- Each card links to the event URL in a new tab.

- [ ] **Step 5: Verify caching behaviour**

- Reload the homepage twice — second load should be sub-second (no upstream call).
- Modify the fixture file and reload — within the cache window (60 min), changes should NOT appear yet. Adjust `cacheDurationMinutes` to 1 on the feed node, wait 1 minute, reload — changes appear.

- [ ] **Step 6: Verify graceful failure**

Temporarily set the feed URL to an obviously-broken URL (`https://localhost:1/nope`). Reload. Expect:
- The block renders the previously-cached events (stale-on-error).
- Or, if no cache exists, the block renders empty (just the headline if set, or nothing if not).
- No 500 error on the page.

Restore the URL.

- [ ] **Step 7: Multi-tenant verification**

Switch tenant root in the content tree. Confirm the block's feed picker on a content node under the new tenant only shows feeds from THAT tenant's `Settings/Feeds` container — not the other tenant's. (If both tenants have feeds with different URLs, picking one should produce that tenant's events.)

- [ ] **Step 8: Remove test artefacts and commit nothing extra**

If `wwwroot/test-feeds/` was used, remove it now:

```bash
rm -rf src/UmbracoCommunity.Web.UI/wwwroot/test-feeds
git status
```

Expect: clean working tree (no leftover test fixtures committed).

---

## Task 12: Run all tests and create the PR

- [ ] **Step 1: Run the full test suite**

```bash
dotnet test
```

Expected: all tests pass.

- [ ] **Step 2: Run the build**

```bash
dotnet build
```

Expected: 0 errors, 0 new warnings.

- [ ] **Step 3: Create the PR**

```bash
gh pr create --base develop --title "Add calendar feed block (Phase 1: umbracalendar)" --body "$(cat <<'EOF'
## Summary
- Adds `calendarFeedBlock` content block that displays upcoming Umbraco community events from a JSON feed.
- Adds `calendarFeed` document type under per-tenant `Settings/Feeds` for backoffice configuration of feed URL + cache duration.
- New `CalendarFeedService` fetches/parses/caches/sorts/filters events with stale-on-error fallback.
- Sets up `UmbracoCommunity.Web.Tests` xUnit project; service layer covered by unit tests.

See [docs/superpowers/specs/2026-04-29-calendar-feed-block-design.md](docs/superpowers/specs/2026-04-29-calendar-feed-block-design.md) for the design rationale. Phase 2 (blog feeds via curated upstream) is deferred.

## Test plan
- [x] `dotnet test` — all unit tests pass
- [ ] Backoffice manual verification: add block, pick feed, verify upcoming events render
- [ ] Verify cancelled events show `is-cancelled` styling
- [ ] Verify HQ-organized badge appears
- [ ] Verify caching: sub-second second reload
- [ ] Verify graceful failure with bad URL
- [ ] Verify multi-tenant feed picker scoping

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-review checklist completion notes

- **Spec coverage:** Backoffice config (Task 7), service layer (Tasks 4–6), block + view (Tasks 8–10), tests (Tasks 1–5), multi-tenant verification (Task 11.7), error handling (Task 5 + Task 11.6), caching (Task 5 + Task 11.5). Phase 2 explicitly out of scope.
- **Spec deviation:** The spec references `IViewModelBuilder<,>` but the existing block convention in this codebase uses `@inject` on the partial. The plan adopts the codebase convention; the DTO/view-model split flagged in the spec collapses to using `CalendarEvent` directly in the view (the future split can be added the moment view-only concerns appear).
- **Type consistency:** `CalendarFeed`, `CalendarEvent`, `AttendanceMode`, `RoundRobinMerger.Merge`, `ICalendarFeedService.GetUpcomingEventsAsync`, `CalendarFeedHttpClient.Client`, `CalendarFeedJsonOptions.Default` are referenced consistently across all tasks.

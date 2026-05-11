# Calendar Feed Block — Design

**Date:** 2026-04-29
**Status:** Proposed
**Phase:** 1 of 2 (calendar only; blog feed block is Phase 2, separate spec when upstream exists)

## Goal

Add a content block that displays a list of upcoming Umbraco community events on the home page (and anywhere else editors place it), sourced from umbracalendar.com.

The block must:
- Pull from one or more named feed sources configured in the backoffice (multi-tenant aware).
- Show the next *N* upcoming events, sorted by start date ascending, past events filtered out.
- Surface event metadata (cancelled, HQ-organized, attendance mode, etc.) to the Razor view so designers can render badges, strikethroughs, and similar treatments.
- Cache feed data and degrade gracefully when the upstream is unavailable.

## Scope

**In scope (Phase 1):**
- Calendar feed source configured under Settings.
- Single block type (`calendarFeedBlock`) that picks calendar feed nodes.
- Backend service that fetches a JSON feed, deserialises to typed records, caches, sorts, merges, and returns a view model.
- Razor partial rendering an event card list.

**Out of scope (Phase 2 / separate work):**
- Blog feed block. Will be built when a curated blog JSON feed exists upstream.
- The JSON-producing endpoint on umbracalendar.com itself. Owned by that project; this spec assumes it exists at the agreed shape (Appendix A).
- AI/relevance filtering of community blog posts. Handled by a future curation service that produces a clean JSON feed.

## Upstream contract

This site consumes JSON, not RSS. umbracalendar.com is expected to expose a JSON endpoint (e.g. `https://umbracalendar.com/meetup.json` or `/v1/meetup.json`) with the shape in Appendix A.

The existing RSS feed at `/meetup/` is unaffected and remains for other consumers.

## Architecture

```
┌──────────────────────────────────────┐
│ Settings/Feeds/<calendarFeed node>   │   per tenant, in backoffice tree
│   name, feedUrl, cacheDurationMinutes │
└───────────────┬──────────────────────┘
                │ picked by
                ▼
┌──────────────────────────────────────┐
│ calendarFeedBlock (block grid item)  │   on any page
│   headline, feeds, maxItems          │
└───────────────┬──────────────────────┘
                │
                ▼
┌──────────────────────────────────────┐
│ CalendarFeedBlockViewModelBuilder    │
│   resolves picked feeds              │
│   calls ICalendarFeedService for each│
│   merges, takes top N                │
└───────────────┬──────────────────────┘
                │
                ▼
┌──────────────────────────────────────┐
│ ICalendarFeedService                 │   per feed node
│   HttpClient + IMemoryCache          │
│   GET JSON → CalendarFeed record     │
│   filter past events                 │
│   sort startsAt ascending            │
│   stale-on-error fallback            │
└──────────────────────────────────────┘
```

## Backoffice configuration

### `feedsContainer` document type
- Container, listable in tree.
- Allowed children: `calendarFeed` (and `blogFeed` in Phase 2).
- Allowed under: `siteSettings` (the existing per-tenant Settings doc type).
- Icon: e.g. `icon-rss` colour-light-blue.

### `calendarFeed` document type
Properties:
| Alias | Editor | Notes |
|---|---|---|
| `name` | Textstring (mandatory) | Display name; shown in pickers. |
| `feedUrl` | Textstring (mandatory) | Full URL of the JSON feed. |
| `cacheDurationMinutes` | Numeric (mandatory, default 60, min 1) | Server cache lifetime. |

Allowed under: `feedsContainer`.

### `calendarFeedBlock` element type (block grid)
Properties:
| Alias | Editor | Notes |
|---|---|---|
| `headline` | Textstring (optional) | Optional title above the list. |
| `feeds` | MultiNodeTreePicker (mandatory, min 1) | Filtered to `calendarFeed` doc type. Dynamic root resolved per-tenant: ancestor of current node → `feedsContainer` child of Settings. |
| `maxItems` | Numeric (mandatory, default 5, min 1, max 50) | Total events shown after merge. |

### `settingsCalendarFeedBlock` settings type
Standard background/spacing toggles consistent with other blocks (mirror `SettingsHeroBannerWithImageSlider`'s shape — exact properties confirmed during implementation by reading existing settings types in `Models/ContentModels/`).

## Service layer

Located in `UmbracoCommunity.Web/Features/Feeds/`.

### Records (DTOs and view models)

```csharp
// JSON deserialisation target — matches Appendix A shape.
public sealed record CalendarFeed(FeedMeta Feed, IReadOnlyList<CalendarEvent> Events);
public sealed record FeedMeta(string Title, string SourceUrl, DateTimeOffset GeneratedAt);
public sealed record CalendarEvent(
    string Id, string Url, string Title, string? Summary,
    DateTimeOffset PublishedAt, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string? Location, string? Organizer,
    AttendanceMode AttendanceMode, bool IsHqOrganized, bool IsCancelled);

public enum AttendanceMode { Unknown, InPerson, Online, Hybrid }
```

`AttendanceMode` is serialised with `JsonStringEnumConverter` + camelCase naming policy so `"inPerson"` round-trips.

### Service interface

```csharp
public interface ICalendarFeedService
{
    Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(
        IPublishedContent feedNode,
        CancellationToken ct = default);
}
```

### Implementation

1. Read `feedUrl` and `cacheDurationMinutes` from the feed node.
2. Cache key: `calendar-feed:{feedNode.Key}`. Check `IMemoryCache`.
3. **On hit:** return cached list.
4. **On miss:**
   - `HttpClient.GetAsync(url)` with 10s timeout (configured on the typed client).
   - `JsonSerializer.Deserialize<CalendarFeed>(stream, options)` with `JsonSerializerDefaults.Web` and `JsonStringEnumConverter`.
   - Filter events where `EndsAt < DateTimeOffset.UtcNow`.
   - Sort remaining by `StartsAt` ascending.
   - Cache for `cacheDurationMinutes`.
   - Also store in a separate "stale fallback" cache entry with a 7-day sliding expiry so we always have something to serve on outage even if the primary entry has expired.
5. **On error (timeout, non-2xx, deserialisation failure):** log warning with feed name + URL + exception, return stale fallback if present, else empty list.

### HTTP client
`IHttpClientFactory` typed client `CalendarFeedHttpClient`:
- 10s timeout.
- `Accept: application/json`.
- User-Agent identifying this site (e.g. `UmbracoCommunitySite/1.0 (+https://...)`).

### Composer
`RegisterFeeds.cs` registers:
- `IMemoryCache` (already present in Umbraco DI; no-op if so).
- `AddHttpClient<CalendarFeedHttpClient>(...)` with the timeout/headers above.
- `services.AddSingleton<ICalendarFeedService, CalendarFeedService>()`.

## View model builder

`UmbracoCommunity.Web/ViewModelBuilders/Blocks/CalendarFeedBlockViewModelBuilder.cs`.

```csharp
public sealed record CalendarFeedBlockViewModel(
    string? Headline,
    IReadOnlyList<CalendarEventViewModel> Events);

public sealed record CalendarEventViewModel(
    string Id, string Url, string Title, string? Summary,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string? Location, string? Organizer,
    AttendanceMode AttendanceMode, bool IsHqOrganized, bool IsCancelled);
```

(View model is currently the same shape as the DTO. Kept as a separate type so view-only concerns — e.g. localised relative dates — can be added later without leaking into the service layer.)

Builder logic:
1. Resolve the feeds picked on the block to `IPublishedContent` instances.
2. Call `ICalendarFeedService.GetUpcomingEventsAsync` for each in parallel via `Task.WhenAll`.
3. **Merge strategy:** round-robin interleave (per Q9 in brainstorming). Each per-feed list is already sorted by `StartsAt` ascending. Round-robin: take the next item from each feed list in turn (skipping exhausted ones) until `maxItems` is reached or all lists are exhausted.
4. Map `CalendarEvent` → `CalendarEventViewModel`.

## Razor partial

`UmbracoCommunity.Web.UI/Views/Partials/Blocks/CalendarFeedBlock.cshtml`.

Receives a `CalendarFeedBlockViewModel`. Renders an event card list. Each card has:
- Title (linked to `Url`).
- Date(s) — formatted from `StartsAt`/`EndsAt`.
- Location and organizer if present.
- `is-cancelled` class on the card if `IsCancelled` is true (CSS handles strikethrough/visual treatment).
- HQ-organized badge if `IsHqOrganized` is true.
- Attendance mode badge (icon for in-person / online / hybrid).

Visual design is intentionally not specified here — the implementation phase will brainstorm/wireframe the card design separately.

## Multi-tenancy

- The block's `feeds` picker uses a dynamic root resolved per-tenant: walk up from the current content's ancestors until a node implementing the per-tenant settings root pattern is found, then locate its `feedsContainer` child. This mirrors the existing pattern used elsewhere (e.g. Authors picker on Article).
- Cache keys use the feed node's `Key` (Guid), which is globally unique across tenants — no collision risk.
- Different tenants can configure different feed URLs (e.g. region-specific calendar feeds) without code changes.

## Error handling & resilience

| Scenario | Behaviour |
|---|---|
| HTTP timeout | Log warning, return stale fallback if any, else empty list. Block renders empty (no error UI). |
| HTTP non-2xx | Log warning, return stale fallback if any, else empty list. |
| Malformed JSON / deserialisation error | Log warning with feed identifier, return stale fallback if any, else empty list. |
| Feed node missing required properties | Log warning, treated as no events for that feed; other feeds in the block still render. |
| Picker references unpublished feed node | Skip silently (consistent with other pickers in this site). |
| All feeds fail | Block renders with headline (if set) and no events. |

The block never throws and never breaks page rendering.

## Testing

**Backend (xUnit):**
- Deserialisation of the Appendix A JSON shape, including all `attendanceMode` values and edge cases (null `Location`, null `Organizer`, missing `Summary`).
- Filtering of past events at the `EndsAt < now` boundary (event ending exactly now: filter out).
- Sorting by `StartsAt` ascending.
- Round-robin merge across 1, 2, and 3 feeds with varying lengths.
- Cache hit/miss behaviour with a fake time provider.
- Stale-on-error fallback when the upstream errors after a successful prior fetch.
- Empty list result when there's never been a successful fetch.

Test project: existing unit test project if any, otherwise add `UmbracoCommunity.Web.Tests` (xUnit).

**Frontend:** none required for Phase 1 — the view is pure Razor.

## Project layout

```
src/UmbracoCommunity.Web/
├── Features/Feeds/
│   ├── CalendarFeed.cs              (records: CalendarFeed, FeedMeta, CalendarEvent, AttendanceMode)
│   ├── ICalendarFeedService.cs
│   ├── CalendarFeedService.cs
│   ├── CalendarFeedHttpClient.cs    (typed HttpClient marker)
│   └── RegisterFeeds.cs             (composer)
├── Models/ContentModels/
│   └── CalendarFeedBlock.cs
├── Models/ViewModels/Blocks/
│   └── CalendarFeedBlockViewModel.cs
└── ViewModelBuilders/Blocks/
    └── CalendarFeedBlockViewModelBuilder.cs

src/UmbracoCommunity.Web.UI/
└── Views/Partials/Blocks/
    └── CalendarFeedBlock.cshtml
```

Backoffice doc-types and element types are configured manually in the Umbraco backoffice; Models Builder is regenerated afterwards.

## Open questions for implementation phase

- Exact properties on `settingsCalendarFeedBlock` — match existing block settings conventions.
- Visual design of the event cards — wireframe during implementation.
- Whether the umbracalendar JSON URL is finalised by the time we start; if not, agree a temporary fixture URL + sample JSON file for development.

## Future: Phase 2 — Blog feed block

Spec'd separately when a curated blog JSON source exists. Expected shape:
- `blogFeed` doc type (sibling to `calendarFeed`) under `feedsContainer`.
- `blogFeedBlock` element type, picks `blogFeed` nodes.
- Service identical in pattern to `CalendarFeedService` — different DTO shape, sort by `publishedAt` descending, no past-event filtering.
- The JSON shape will be defined alongside the curation service and added to a Phase 2 spec.

---

## Appendix A — JSON feed shape (calendar)

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
      "summary": "Optional freeform notes; may be null.",
      "publishedAt": "2026-01-11T22:30:51Z",
      "startsAt": "2026-07-16T18:00:00+10:00",
      "endsAt": "2026-07-16T19:30:00+10:00",
      "location": "Luminary, Level 1, 195 Little Collins Street, Melbourne, AU",
      "organizer": "Umbraco Melbourne Meetup",
      "attendanceMode": "inPerson",
      "isHqOrganized": false,
      "isCancelled": false
    }
  ]
}
```

**Conventions:**
- All field names camelCase.
- `id` is a string (the upstream RSS mixes numeric Meetup.com IDs and GUIDs; consumers should treat as opaque).
- All dates ISO 8601 with offset. `startsAt` and `endsAt` keep the event's original timezone offset for local-time rendering. `publishedAt` and `generatedAt` are UTC (`Z`).
- `attendanceMode` ∈ `"inPerson" | "online" | "hybrid" | "unknown"`.
- `isHqOrganized` and `isCancelled` are real booleans, not strings.
- `summary`, `location`, `organizer` may be `null` or empty string.
- Pretty-printed at the source for debuggability; gzip handles the bytes.
- Versioning via path (e.g. `/v1/meetup.json`) if the shape ever changes incompatibly.

Pre-existing source-side data fix: the current RSS uses `meetup` as a value where `physical`/`online`/`hybrid` was meant. To be cleaned up in the JSON output (mapped to the correct attendance mode based on each event's actual nature).

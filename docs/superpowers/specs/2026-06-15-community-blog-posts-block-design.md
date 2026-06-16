# Community Blog Posts Block — Design

**Date:** 2026-06-15
**Status:** Approved (pending spec review)

## Goal

Recreate the old site's [community blog posts listing](https://umbraco-community.euwest01.umbraco.io/learn-about-the-community/blog-posts/)
as a Block Grid block on the new Umbraco Community Site. The block renders a **full,
server-paginated listing** of community blog posts, sourced from the **Umbraco Sphere
public API**, reusing the existing blog-card visual language.

## Data source

Umbraco Sphere public API ([swagger](https://sphere.umbraco.com/api/v1/docs)):

- **Endpoint:** `GET https://sphere.umbraco.com/api/v1/blog-posts`
- **Auth:** `Authorization: <apiKey>` header (a bare `psk_...` key — **not** `Bearer`-prefixed).
- **Query params:** `from` / `to` (ISO date bounds), `cursor`, `limit` (default 50, max 100).
- **Pagination:** cursor-based — response carries `pagination.nextCursor` + `pagination.hasMore`;
  pass `nextCursor` back as `?cursor=` for the next page. Newest-first.
- **Response shape** (`PostsResponseDto`):

```jsonc
{
  "data": [
    {
      "id": "58c35289-50d3-448e-abce-8e0c314d2d54",
      "type": "blog_post",
      "title": "Codegarden: What to bring (2026 edition!)",
      "url": "https://joe.gl/ombek/blog/codegarden-packing-list/",
      "content": "Wow, hasn't time flown?! ...",   // excerpt, may be null
      "coverImageUrl": "https://joe.gl/media/.../image.png",  // may be null
      "publishedAt": "2026-06-04T10:14:30.000Z",
      "author": {
        "name": "Blog - Joe Glombek",     // source/blog name; may be null
        "profileUrl": "https://joe.gl/ombek/blog/rss",  // may be null
        "avatarUrl": "https://joe.gl/favicon.ico"        // logo/avatar; may be null
      }
    }
  ],
  "pagination": { "nextCursor": "<opaque|null>", "hasMore": true }
}
```

The `/v1/blog-posts` endpoint returns only `blog_post` items (there is a separate
`/v1/linkedin-posts` endpoint we do **not** use).

### Secrets

The API key must **never** be committed. For local dev it goes in `appsettings.Local.json`
(gitignored); `appsettings.json` ships an empty `ApiKey`. In production it's supplied via
environment/server config. Note: `appsettings.Local.json` only loads under the `Local` launch
profile, so local testing runs that profile.

## Scope

**In scope (v1):**
- Pull all community blog posts from the Sphere API (walking cursors).
- A Block Grid block that lists posts, newest first, with server-side `?page=N` pagination.
- Background aggregation (off the request path) with resilient cached reads + disk fallback.
- Per-post image with fallback (`coverImageUrl` → author `avatarUrl` → placeholder).
- Backend unit tests for cursor-walking / mapping / pagination / resilience.

**Out of scope (v1):**
- LinkedIn posts (`/v1/linkedin-posts`).
- By-tag / author filtering, date-range UI.
- Any client-side JS (load-more / infinite scroll). Pagination is plain `?page=` links.
- Downloading/rehosting images — we reference the external URLs directly.

## Architecture

New self-contained sub-feature under the existing Feeds module, following
`Features/Feeds/Calendar/` (typed `HttpClient`, `TimeProvider`, `IOptionsMonitor`,
memory-cache + stale fallback) and the Sessionize disk-cache + background-refresh convention.

```
Features/Feeds/CommunityBlogs/
  CommunityBlogPost.cs                   // mapped post: Title, Url, Excerpt, CoverImageUrl,
                                         //   PublishedAt, AuthorName, AuthorAvatarUrl, AuthorProfileUrl
  CommunityBlogsData.cs                  // { Posts: IReadOnlyList<CommunityBlogPost>, LastUpdatedUtc }
  SphereBlogPostsDtos.cs                 // PostsResponseDto / PublicPostDto / PublicAuthorDto / PaginationDto
  CommunityBlogsOptions.cs               // bound config (SectionName = "CommunityBlogs")
  SphereApiClient.cs                     // typed HttpClient; GetPageAsync(cursor, limit, ct)
  CommunityBlogsAggregator.cs           // walk cursors -> map -> cap -> persist JSON
  CommunityBlogsBackgroundService.cs     // BackgroundService: aggregate on startup + every N hours
  ICommunityBlogsService.cs
  CommunityBlogsService.cs               // read path: memory cache -> disk -> stale fallback; paginate
Features/Feeds/Configuration/RegisterFeeds.cs   // EXTENDED with the new registrations
```

Block (in the Web project, following existing block conventions):

```
Models/ContentModels/CommunityBlogPostsBlock.cs            // hand-written content model (IContentBlockIntro)
Models/PublishedModels/CommunityBlogPostsBlock.generated.cs        // Models Builder (manual regen)
Models/PublishedModels/SettingsCommunityBlogPostsBlock.generated.cs
Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml       // (in UmbracoCommunity.Web.UI)
StaticAssets/src/css/blocks/community-blogs-block.css
```

### Components

**`CommunityBlogsOptions`** — bound from `appsettings.json`:

```json
"CommunityBlogs": {
  "ApiBaseUrl": "https://sphere.umbraco.com/api/v1/",
  "ApiKey": "",
  "RefreshIntervalInHours": 6,
  "RequestTimeoutSeconds": 15,
  "PageSize": 100,
  "MaxPosts": 1000
}
```

(`ApiKey` is supplied via `appsettings.Local.json` / env, never committed.)

**`SphereApiClient`** — typed `HttpClient` (base address `ApiBaseUrl`, `Authorization` header
set per request from options so key changes don't require restart, JSON `Accept`, UA string,
timeout from `RequestTimeoutSeconds`). One method:
`Task<PostsResponseDto> GetBlogPostsAsync(string? cursor, int limit, CancellationToken ct)`.

**`CommunityBlogsAggregator`** (off the request path):
1. If `ApiKey` is blank, log a warning and abort (keep any existing disk data).
2. Walk cursors: call `GetBlogPostsAsync(cursor, PageSize)` repeatedly, following
   `pagination.nextCursor` while `hasMore` is true, stopping at `MaxPosts` or when a page
   fails. Guard against a repeated/identical cursor (defensive loop break).
3. Map each `PublicPostDto` → `CommunityBlogPost` (trim/normalise; tolerate null
   `content`/`coverImageUrl`/`author` fields; skip items with no `url`).
4. Order newest-first by `PublishedAt` (the API is already desc; we re-sort defensively).
5. Serialise `CommunityBlogsData` and write to
   `umbraco/Data/TEMP/CommunityBlogsCache/community-blogs.json` (Sessionize disk-cache
   location convention). Write is best-effort; failures are logged, not fatal.

**`CommunityBlogsBackgroundService : BackgroundService`** — runs the aggregator once at
startup, then on a `PeriodicTimer(RefreshIntervalInHours)`. All network work happens here.

**`CommunityBlogsService`** (read path used by the view):
- `Task<CommunityBlogsData> GetAllAsync(ct)` — memory cache (primary key, short duration) →
  read disk file → stale in-memory fallback (long sliding expiry), mirroring
  `CalendarFeedService` + `SessionizeApiClient`. Returns empty data (not throwing) if nothing
  is available yet.
- `PagedCommunityBlogPosts GetPage(CommunityBlogsData data, int page, int pageSize)` — offset
  slice returning `{ Items, Page, PageSize, TotalItems, TotalPages }`, page clamped to
  `[1, TotalPages]`.

### Block configuration

Element type `communityBlogPostsBlock`:
- `Title` (string) + `Subtitle` (richtext) via the existing **`IContentBlockIntro`** mixin
  (same as `BlogShowcaseBlock`).

Settings type `SettingsCommunityBlogPostsBlock`, mixing in **`ISettingsBlockId`** +
**`ISettingsColour`** (same as `BlogShowcaseBlock`), plus:
- `PostsPerPage` (int, default 12).

### View & rendering

`Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml`:
- Inherits `UmbracoViewPage<BlockGridItem<CommunityBlogPostsBlock, SettingsCommunityBlogPostsBlock>>`.
- Injects `ICommunityBlogsService` and `IHttpContextAccessor` (for `?page=`).
- Reads `?page=N` (default 1), loads cached data, calls `GetPage(data, page, settings.PostsPerPage)`.
- Renders the intro (Title/Subtitle) + a card grid + a server-rendered pagination control
  (Prev / numbered pages / Next as `?page=` anchor links, preserving the current path).
- Each card (reuses the `dc-blog-showcase-block` card styling, namespaced to a new
  `dc-community-blogs-block` block class):
  - Whole card is an `<a href="{post.Url}" target="_blank" rel="noopener noreferrer">`.
  - Main media: `CoverImageUrl` as the full card image (`loading="lazy"`); when null the
    media figure is omitted (no placeholder asset dependency).
  - Author avatar badge: `AuthorAvatarUrl` rendered as a small rounded "owner" badge
    overlapping the card (like the old site's `card-owner` logo), shown when present —
    independent of the cover image.
  - Title (`h3`), excerpt (truncated via existing CSS line-clamp; omitted when null).
  - Meta line: `{PublishedAt:d MMMM yyyy} by {AuthorName}` (author omitted when null).
- Empty state (no data yet / key unset on first boot): the old site's friendly message —
  *"Unfortunately we are currently unable to display the community blog posts. Please try
  again later."*

### CSS

`community-blogs-block.css` reuses the existing blog-card layout (grid, hover image-zoom,
rounded corners, dark/light `has-bg` rules) from `blog-showcase-block.css`, adding the author
avatar "owner" badge treatment (small rounded image overlapping the card media/header, mirroring
the old site's `card-owner`). Mobile-first with the standard `--sm`/`--md`/`--lg`/`--xl`
breakpoints. Registered in `blocks.css`.

## Data flow

```
startup / 6h timer
  └─ CommunityBlogsBackgroundService
       └─ CommunityBlogsAggregator
            ├─ SphereApiClient.GetBlogPostsAsync(cursor, limit)   (walk cursors, cap at MaxPosts)
            ├─ map DTOs -> CommunityBlogPost, sort newest-first
            └─ write community-blogs.json (TEMP)

page request (?page=N)
  └─ CommunityBlogPostsBlock.cshtml
       └─ ICommunityBlogsService.GetAllAsync  (memory → disk → stale)
            └─ GetPage(page, pageSize) → cards + pagination
```

## Resilience / error handling

- Page requests perform **no** network I/O — they read cached/disk data only.
- A failed API page stops the walk but keeps whatever was already collected; if the very
  first page fails, the previous `community-blogs.json` is retained.
- Stale data keeps rendering if a later refresh fails (long sliding fallback, per Calendar/Sessionize).
- Missing/blank `ApiKey` → aggregation no-ops with a warning; block shows the empty state.
- Malformed/partial DTO items are skipped rather than aborting the run.

## SEO

Pagination uses `?page=N`. Per CLAUDE.md the `MetaTags` ViewComponent already emits canonical
+ `rel="prev"`/`rel="next"` links for paginated URLs, so the listing is SEO-correct without
extra work. Outbound post links use `rel="noopener noreferrer"`.

## Configuration changes

- Add the `CommunityBlogs` section to `appsettings.json` (empty `ApiKey`); real key goes in
  `appsettings.Local.json` (gitignored) for dev and env/server config for production.
- Register options, typed `HttpClient` (`SphereApiClient`), aggregator, background service,
  and `ICommunityBlogsService` in `RegisterFeeds`.
- No new NuGet packages (the RSS/`System.ServiceModel.Syndication` dependency from the earlier
  RSS-based draft is dropped — the API returns normalised JSON).

## Testing

Backend unit tests in the existing `tests/UmbracoCommunity.Web.Tests` project (xUnit + Moq +
FluentAssertions + `StubHandler` + `TestData/**.json`, same harness as `CalendarFeedServiceTests`):

- **DTO mapping** — a canned `PostsResponseDto` page maps to `CommunityBlogPost`s; null
  `content`/`coverImageUrl`/`author` tolerated; items without a `url` skipped.
- **Cursor walking** — stubbed multi-page sequence (page1 `hasMore:true`+cursor → page2
  `hasMore:false`) aggregates across pages; `MaxPosts` cap halts the walk; a repeated cursor
  breaks the loop defensively.
- **Auth/headers** — `StubHandler` asserts the `Authorization` header carries the configured key.
- **Pagination** — `GetPage` returns the correct slice, `TotalPages`, and clamps out-of-range pages.
- **Resilience** — empty/missing disk file returns empty data without throwing; a failing
  refresh falls back to last-known (stale) data; blank `ApiKey` no-ops cleanly.

Block markup is verified manually in the backoffice + on a page (no JS to unit test).

## Open follow-ups (not v1)

- LinkedIn posts feed / combined view.
- By-tag / author / date-range filtering.
- Optional "load more" / infinite scroll if pagination UX needs it.
- Surfacing aggregation health (last-updated / post count) in the backoffice.

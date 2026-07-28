# Community Blog Posts — Featured mode, full listing & search ingestion

Date: 2026-06-16
Status: Approved (design)

Refines the Community Blog Posts block (added in `37452fe`) with three changes:
a **featured mode** with a "Read more" link, a **regular-blog-style pager** on the
full listing, and **Examine ingestion** of the cached posts so they surface in site
search (clearly marked as external links).

## Background

Community blog posts are **not** Umbraco content nodes. They are aggregated from the
Umbraco Sphere public API and cached (in-memory + atomic disk JSON, capped at
`MaxPosts = 60`, refreshed every 6h) by `CommunityBlogsService`. The existing
`CommunityBlogPostsBlock` is a server-rendered Block Grid block that already paginates
via `?page=N` over `ICommunityBlogsService.GetPage(page, pageSize)`.

The block grid already renders 3 columns at the `--lg` breakpoint, so the "full listing"
visual is already in place; the work is mode-switching, link wiring, pager restyle, and
search.

## 1. Featured mode on the block

The same block drives both the featured teaser (Image #12) and the full paged listing
(Image #13). A toggle switches between them — no new document type.

**`SettingsCommunityBlogPostsBlock`** (settings element type) gains:
- `featuredOnly` — `Umbraco.TrueFalse`, default `false`. Label: "Show featured posts only (no paging)".
- `numberOfFeaturedPosts` — integer, **default `6`**, capped at `12` in the view. Only meaningful when `featuredOnly` is on.

**`CommunityBlogPostsBlock`** (content element type) gains:
- `readMoreLink` — `Umbraco.MultiUrlPicker`, max 1. The "Read more" target the editor picks (multi-tenant safe — no tree-walking). Rendered only in featured mode.

**View (`CommunityBlogPostsBlock.cshtml`):**
- **Featured mode** (`featuredOnly == true`): fetch `GetPage(1, Clamp(numberOfFeaturedPosts, 1, 12))`; render the existing card grid; **no pager**. Below the grid, render `readMoreLink` as a right-aligned plain text link (same treatment as the Blog Showcase block's "Read more on the blog" CTA — picks up the global `#main-content a:not(.btn)` pink-underline animation). Link text: the picked link's `Name`, falling back to "Read more on the blog". If no link is picked, render nothing.
- **Full mode** (default): unchanged paged behaviour using `PostsPerPage`, with the restyled pager (section 2).

Models Builder partials (`CommunityBlogPostsBlock.generated.cs`,
`SettingsCommunityBlogPostsBlock.generated.cs`) regenerated after the element-type edits.
Umbraco Deploy `.uda` artifacts for the two element types updated.

## 2. Pager restyle (full mode)

Replace the numbered page links in `.dc-community-blogs__pagination` with the
First / Previous / `Page X of Y` / Next / Last pattern used by `SearchPage.cshtml` and the
regular blog, including disabled `<span>` markers for unavailable ends. Keep the existing
`.dc-community-blogs__page-link` classes and the `bg-dark` variant; `BuildPageUrl` is
unchanged. CSS in `community-blogs-block.css` adjusted only as needed for the new markup.

## 3. Examine ingestion → site search

**New dedicated Examine index `CommunityBlogsIndex`:**
- Registered in `Features/Feeds/Configuration/RegisterFeeds.cs` via
  `services.AddExamineLuceneIndex("CommunityBlogsIndex")`.
- New `ICommunityBlogsIndexer` / `CommunityBlogsSearchIndexer`: given `CommunityBlogsData`,
  **rebuilds** the index (clear, then one `ValueSet` per post) with fields `title`,
  `excerpt`, `author`, `url`, `publishedAt`, keyed by the post `Id`. Rebuild (not
  incremental) is fine — the set is small and fully replaced each refresh.
- `CommunityBlogsService.RefreshAsync` calls the indexer after a successful aggregate +
  image-localize, so memory cache, disk cache, and the index always move together. The
  6-hourly `CommunityBlogsBackgroundService` already drives `RefreshAsync`.

**`SearchService` changes:**
- After the existing content query, run a second `ManagedQuery` against `CommunityBlogsIndex`
  over `title`/`excerpt`/`author` (guard with `TryGetIndex`; missing index → skip, no error).
- Map community hits to `SearchResultItem` using the post's external `url` and excerpt.
- Community results are **global** — not tenant-filtered (they are external links, tenant-agnostic).
- **Merge**: combine content + community results ordered by raw Lucene score descending, then
  apply `skip`/`take` over the combined set so totals stay accurate. (Cross-index scores are
  only roughly comparable; both are Lucene/BM25-ish, which is acceptable. Revisit if ordering
  feels off in practice.)

**`SearchResultItem`** gains `bool IsExternal` (default `false`; `true` for community posts).

**`SearchPage.cshtml`**: external results render with `target="_blank" rel="noopener noreferrer"`
and a visible "External link ↗" indicator near the title/URL so it is clear the result leaves
the site. CSS for the indicator added to the search page stylesheet.

## 4. Testing

- `CommunityBlogsServiceTests`: assert `RefreshAsync` invokes `ICommunityBlogsIndexer` with the
  refreshed data (mock the indexer); assert no index call when aggregation yields no data.
- `GetPage(1, n)` featured-fetch assertion (clamping to 1..12).
- New `CommunityBlogsSearchIndexer` test: build against an in-memory Lucene index, query, and
  assert expected hits / fields.

## Out of scope

- No new page document type (full listing is the block in full mode on a content page).
- No change to the Sphere API client, aggregator, image downloader, or 6h refresh cadence.
- No change to AI Search (404 suggestions) — site search remains Examine.

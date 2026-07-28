---
tags: [primer, caching, performance, architecture]
---

# Caching primer

Caching in this codebase isn't one system — it's a dozen small decisions made in different places, each reaching for whatever cache fit that problem. That's fine, but it means "how is X cached?" has no single answer until you know *which kind of thing* X is. This primer maps the caches to the problems they solve, so you can tell which one to reach for (and what invalidates it) without spelunking.

> Just want the lookup table? Skip to [the map](#the-map).

## The four questions

Almost every cache here is answering one of four questions. Find your question and you've found your cache.

### 1. "This is deterministic output that rarely changes."

Rendering artefacts that are stable most of the time — cache them hard, and invalidate structurally (watch the file) or with a long TTL as a backstop.

- **The Vite manifest** ([`Vite/TagHelpers/ViteTagHelperBase.cs`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteTagHelperBase.cs)) — the entry-name→hashed-asset map is read once into a static `IMemoryCache` and invalidated by **watching the file** (`IFileProvider.Watch`), not by a clock. A deploy swaps `manifest.json`, the watcher fires, the next request reloads it. No TTL, no restart. (See the [frontend primer](frontend.md) for the dev/prod side of this.)
- **Scoped inline SVG** ([`TagHelpers/SvgTagHelper.cs`](../../src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs)) — the sanitised, style-scoped SVG markup is deterministic per media path, so it's cached in Umbraco's `AppCaches.RuntimeCache` under `svg-scoped::{path}` for 60 minutes. There's no media-change hook, so the 60-minute TTL is the backstop that lets a re-uploaded logo eventually show. This is the one place we use Umbraco's own cache abstraction rather than `IMemoryCache`. → The whole pattern is the [caching scoped SVG output](../tutorials/refinements/caching-scoped-svg-output.md) tutorial.

### 2. "This is an API response I can serve stale for a while."

HTTP endpoint responses, cached by ASP.NET Core's **output caching** with named policies defined in [`Extensions/UmbracoBuilderExtensions.cs`](../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs) and bound to config via `OutputCacheOptions`. Endpoints opt in with `[OutputCache(PolicyName = …)]`. Two policies, two invalidation strategies:

- **`ContentDriven`** — for endpoints backed by Umbraco content (the blog API). Long expiry, but **tag-evicted**: responses are tagged `blog-content`, and [`Notifications/BlogContentCacheInvalidationHandler`](../../src/UmbracoCommunity.Web/Notifications/BlogContentCacheInvalidationHandler.cs) calls `EvictByTagAsync` when a relevant content type is published or unpublished. So edits show up immediately, not after the TTL.
- **`ExternalApi`** — for data we don't control (Sessionize). Short, purely time-based expiry — there's no publish event to hang an eviction off, so you just accept a few minutes of staleness.

→ When `[OutputCache]` beats `ResponseCaching`, and how to be a polite consumer of a slow upstream, is the planned [output-cache-policies tutorial](../tutorials/refinements/output-cache-policies-for-slow-upstream-apis.md).

### 3. "This is expensive to compute and I'll need it again this request — or next."

In-memory `IMemoryCache`, keyed by the inputs, invalidated when the underlying data changes. The richest example is **Block Restrictions**, which runs *two* tiers:

- a per-rule cache (30-minute sliding) so a content-tree walk doesn't hit the database at every ancestor, and
- a per-node *resolved-result* cache (60-second absolute) so a page with several restricted editors does the walk once.

The clever bit is invalidation: editing one rule can change the answer for every descendant node, so rather than track which nodes to evict, a static **generation counter** is baked into the cache key and bumped — every old key becomes unreachable at once (the orphaned entries are never read again and age out by TTL). → That's the subject of the [content-tree-inherited config](../tutorials/foundations/content-tree-inherited-config.md) foundation; it's worth reading even if you never touch block restrictions, because the counter trick transfers.

### 4. "This comes from a slow or flaky third party and the page must not die when it's down."

The integration feeds all share one resilience shape: an in-memory cache for the happy path, backed by a **stale fallback** (a longer-lived copy, often persisted to disk) that gets served when the upstream errors. Better a few hours stale than a 500.

- **Sessionize** ([`Features/Sessionize/Infrastructure/SessionizeApiClient.cs`](../../src/UmbracoCommunity.Web/Features/Sessionize/Infrastructure/SessionizeApiClient.cs)) — `IMemoryCache` keyed `sessionize_all_{eventId}` for `CacheDurationInMinutes`, backed by an on-disk JSON fallback that's served whenever the API is unreachable (no age limit — stale data beats no programme).
- **Community Blogs** and **Calendar Feed** ([`Features/Feeds/`](../../src/UmbracoCommunity.Web/Features/Feeds/)) — the same primary-plus-stale shape, but with a bounded stale window: Community Blogs adds a background refresh service and a disk copy kept for 30 days; Calendar keeps a 7-day stale copy.

→ The [integrations primer](integrations.md) covers these feeds from the *integration* angle; this entry is just their caching shape.

## A couple that don't fit the four

- **404 suggestions** ([`Services/PageNotFoundSuggestionService.cs`](../../src/UmbracoCommunity.Web/Services/PageNotFoundSuggestionService.cs)) — caches suggestions per `{tenant}:{culture}:{max}:{query}` with an **outcome-dependent TTL** (a day for hits, an hour for empty results, a minute for failures) plus a concurrency limiter so a search outage can't stampede the index.
- **404-tracker ignore rules** ([`Umbraco.Community.NotFoundTracker/Matching/`](../../src/Umbraco.Community.NotFoundTracker/Matching/)) — not a keyed cache at all but an **immutable snapshot** held behind a volatile reference and atomically swapped on change. No TTL, no per-lookup cost; the whole rule set is rebuilt and replaced when a rule mutates.

## The map

| Caching it because… | Mechanism | Where | Lifetime | Invalidated by |
| --- | --- | --- | --- | --- |
| Built at deploy time (assets) | static `IMemoryCache` | Vite manifest | until file changes | `IFileProvider.Watch` |
| Deterministic render output | `AppCaches.RuntimeCache` | SVG TagHelper | 60 min | TTL |
| Content-backed API response | OutputCache `ContentDriven` | blog API | long (config) | tag eviction on publish |
| Uncontrolled API response | OutputCache `ExternalApi` | Sessionize API | minutes (config) | TTL |
| Expensive cross-request compute | `IMemoryCache` ×2 | Block Restrictions | 30 min sliding / 60 s | per-key remove / generation counter |
| Slow third-party data | `IMemoryCache` + disk/stale | Sessionize, feeds | config + long stale | TTL; stale served on error |
| Per-query 404 suggestions | `IMemoryCache` | suggestion service | 1 d / 1 h / 1 min | outcome-based TTL |
| Match rules, read-hot | volatile snapshot | NotFoundTracker | none | atomic swap on change |

## Choosing one

- **Deterministic output of a pure-ish function?** Cache it by its inputs (`IMemoryCache`, or `RuntimeCache` if it's Umbraco-content-derived). The SVG and Vite caches are this.
- **An HTTP endpoint?** Reach for `[OutputCache]` with a policy — don't hand-roll an in-memory cache in the controller. Tag it if a content event can invalidate it; time-box it if nothing can.
- **Something a write should invalidate broadly?** Don't enumerate what to evict — put a generation counter in the key (Block Restrictions) and let the old entries age out.
- **Third-party data?** Always pair the cache with a stale fallback. The question isn't "how fresh" but "what do I serve when it's down."

A note on what we *don't* use: Umbraco also offers `AppCaches.RequestCache` (per-request memoisation) and `IsolatedCaches` (per-content-type). They're reasonable tools, but nothing in this codebase reaches for them today — so if you're tempted, you're breaking new ground, not following a pattern.

## Related docs

- **[Caching the scoped SVG output](../tutorials/refinements/caching-scoped-svg-output.md)** — `RuntimeCache` keyed by media path, and why the output is deterministic enough to cache.
- **[Configuration that inherits down the content tree](../tutorials/foundations/content-tree-inherited-config.md)** — the two-tier cache and the generation-counter invalidation trick, in full.
- **[Backend primer → Output caching](backend.md#output-caching)** — where the policies sit in the bootstrap and request flow.
- **[Integrations primer](integrations.md)** — the third-party feeds whose stale-fallback caching appears under question 4.

Cache to the shape of the problem, invalidate on the event that actually changes the answer, and you'll stay out of the two classic traps — serving stale forever, and caching nothing at all.

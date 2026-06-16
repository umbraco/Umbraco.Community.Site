---
tags: [primer, integrations, sessionize, third-party, http-client]
---

# Third-party integrations primer

The external dependencies this site talks to fall into two groups, and the split is worth holding in your head: a handful of **server-side data feeds** that pull content from someone else's API, and a thin **frontend layer** for consent and analytics. The feeds are where the substance is, and the good news is they're all the same shape — learn one and you can read or add any of them.

> Just want the inventory? Skip to [the map](#the-map).

## The server-side feed pattern

Every server-side integration is a `Features/<Name>/` module (see the [backend primer](backend.md#self-contained-features) for why features get their own folder) built from the same four parts:

1. **An options class** bound to an `appsettings.json` section — base URL, cache duration, any keys.
2. **A named `HttpClient`** with a sane timeout and a `User-Agent`, registered in the feature's composer.
3. **A service** that fetches, deserialises, and **caches with a stale fallback** — so a slow or down upstream degrades to slightly-old data instead of a 500. (That caching shape is question 4 in the [caching primer](caching.md#4-this-comes-from-a-slow-or-flaky-third-party-and-the-page-must-not-die-when-its-down).)
4. **A composer** — an Umbraco `IComposer`, the startup hook where a feature registers its own services (`RegisterSessionize`, `RegisterFeeds`) — that wires the three together. `AddComposers()` discovers it; nothing else has to know the feature exists.

The three feeds:

- **Sessionize** ([`Features/Sessionize/`](../../src/UmbracoCommunity.Web/Features/Sessionize/)) — conference programme, speakers, and schedule from the Sessionize platform. Config section `Sessionize` (`EventId`, `BaseUrl`, `CacheDurationInMinutes`); `EventId` is the one bit you must supply per environment. Unlike the other two, it's surfaced to the browser through `/api/sessionize/*` endpoints (output-cached with the `ExternalApi` policy) and rendered by Lit components — so it has a frontend half too. `SessionizeApiClient` keeps an in-memory cache *and* an on-disk fallback for when the API is unreachable. This is the most-documented integration: see the Sessionize section of [`CLAUDE.md`](../../CLAUDE.md) for the endpoint and component map.
- **Calendar Feed** ([`Features/Feeds/Calendar/`](../../src/UmbracoCommunity.Web/Features/Feeds/Calendar/)) — community events from `umbracalendar.com`. Config section `CalendarFeed` (`Url`, `CacheDurationInMinutes`). A public endpoint, no key; `CalendarFeedService` keeps a 7-day stale copy. Rendered server-side, not exposed as an API.
- **Community Blogs** ([`Features/Feeds/CommunityBlogs/`](../../src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/)) — aggregated community blog posts from Umbraco's Sphere API (the community-content aggregation service). The richest of the three: a `CommunityBlogsBackgroundService` refreshes on an interval (`RefreshIntervalInHours`), `SphereApiClient` pages through results, and `CommunityBlogsImageDownloader` pulls featured images on its own `HttpClient`. Config section `CommunityBlogs` — note the `ApiKey`, which is a **secret**: it's not in the committed `appsettings.json`, so set it in `appsettings.Local.json` (or the Cloud portal). Rendered server-side; caches in memory with a disk copy and a 30-day stale fallback.

Both Calendar and Community Blogs register through one composer, [`Features/Feeds/Configuration/RegisterFeeds.cs`](../../src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs).

## The frontend layer

Two browser-side integrations, both in [`StaticAssets/src/integrations/`](../../src/UmbracoCommunity.StaticAssets/src/integrations/) or the shared entrypoint:

- **GTM (server-side tagging)** — analytics is live in production. [`Views/_ViewImports.cshtml`](../../src/UmbracoCommunity.Web.UI/Views/_ViewImports.cshtml) injects an Umbraco server-side-tagging GTM container (`load.sst.umbraco.com`, id `GTM-T6TKMT2`) under an `IsProduction()` guard, and [`entrypoints/_index.ts`](../../src/UmbracoCommunity.StaticAssets/src/entrypoints/_index.ts) feeds it — a global click handler that pushes `gtm.linkClick` events (the clicked element's url, text, classes, id) onto `window.dataLayer`. Nothing to enable locally: the container only loads in production.
- **Cookiebot** ([`integrations/cookiebot.element.ts`](../../src/UmbracoCommunity.StaticAssets/src/integrations/cookiebot.element.ts)) — a consent-management custom element (`<dc-cookiebot>`, extending a small `script-loader.element.ts` base that injects the third-party script; the account id is hardcoded). **It's registered but not currently mounted in any view** — a dormant remnant from an earlier build, available to wire up if the site needs a consent banner, rather than active consent management today.

## Avatars and build-time

Worth knowing they exist, though they're hot-linked CDN/build-time bits rather than configured services:

- **Member/MVP avatars** ([`Features/Mvp/Infrastructure/MvpDataService.cs`](../../src/UmbracoCommunity.Web/Features/Mvp/Infrastructure/MvpDataService.cs)) — avatars hot-link to GitHub (`github.com/{handle}.png`) when an MVP has a GitHub handle, falling back to a deterministic DiceBear avatar (`api.dicebear.com/.../{seed}`) when they don't. No config, no auth — public image URLs.
- **Doc contributors** ([`StaticAssets/devops/generate-doc-contributors.js`](../../src/UmbracoCommunity.StaticAssets/devops/generate-doc-contributors.js)) — a *build-time* script that reads git history for the docs and, if a `GITHUB_TOKEN` is present in CI, resolves commit emails to GitHub accounts for the avatar chips on each rendered doc. Runs in CI, not at request time.

## The map

| Integration | Where | Type | Config | Key/secret? |
| --- | --- | --- | --- | --- |
| Sessionize | `Features/Sessionize/` | backend feed + `/api/sessionize` + Lit | `Sessionize` section | `EventId` per env |
| Calendar Feed | `Features/Feeds/Calendar/` | backend feed (server-rendered) | `CalendarFeed` section | none (public) |
| Community Blogs | `Features/Feeds/CommunityBlogs/` | backend feed + background service | `CommunityBlogs` section | `ApiKey` (Local/portal) |
| GTM (server-side tagging) | `_ViewImports.cshtml` (container) + `_index.ts` (data layer) | frontend analytics, prod-only | SST container `GTM-T6TKMT2` | — |
| Cookiebot | `StaticAssets/.../integrations/` | frontend consent — *registered, not mounted* | hardcoded account id | — |
| MVP / contributor avatars | `Features/Mvp/`, `devops/` | CDN hot-link / build-time | none / `GITHUB_TOKEN` (CI) | — |

## What isn't here (so you don't go looking)

The project's top-level description and some older docs mention integrations that **aren't in this repo**: Matomo, Intercom, and a Google Maps community map were never wired up (or have been removed — the `integrations/` folder holds only Cookiebot today; the `@googlemaps/*` npm packages linger in `package.json` but no code uses them), and the **GitHub release-tracking** feature was extracted into a separate releases site. If you're hunting for any of those here, stop — they live elsewhere or not at all.

## Adding a new integration

For anything server-side that calls an external API, follow the feed pattern above rather than inventing a new one: an options class bound to an `appsettings` section, a named `HttpClient` with a timeout, a service that caches with a stale fallback, and a composer to wire it up — all under `Features/<Name>/`. Put secrets in `appsettings.Local.json` (gitignored), never in the committed config. Promote to a `Features/` folder once there's a client + options + service + DTOs that belong together; see the [backend primer](backend.md#self-contained-features) for the threshold.

## Related docs

- **[Backend primer → Self-contained features](backend.md#self-contained-features)** — the `Features/<Name>/` module shape these all follow.
- **[Caching primer](caching.md)** — the stale-fallback caching that makes the feeds resilient (question 4).
- **[`CLAUDE.md`](../../CLAUDE.md)** — the Sessionize feature in depth: endpoints, frontend components, deep-linking, and Open Graph.

Learn the feed shape once and the rest of the server-side integrations read the same; the frontend layer is just consent plus a data-layer ping. Adding one is mostly a matter of not being clever — copy the pattern, keep the secret out of git, cache with a fallback.

# Primer ideas

A backlog of candidate primers for future areas of the codebase. Each entry frames what the primer would orient readers to and what threads it would tie together. Prune entries that stop seeming worth writing; add new ones as you hit them.

These aren't tutorials — see [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md) for those. Primers are concept-oriented overviews; tutorials are deep dives. A primer should know what tutorials exist and link to them.

## Top picks

If you only write three more, these are the ones with the strongest "broad area with scattered docs that need threading" shape:

- ~~**`backend`**~~ — shipped as [backend.md](./backend.md).
- **`multi-tenancy`** — the [multi-tenant content resolution tutorial](../tutorials/foundations/multi-tenant-content-resolution.md) is the foundation, and the two refinements (404, schema) layer on it, but there's no top-level "here's what tenancy looks like in this codebase" overview that someone unfamiliar can read in five minutes. Primer would frame the multi-root content tree, the `GetSiteSettings()` convention, the domain-binding rule for routing-level lookups, and link out to the tutorial suite for depth.
- **`backoffice`** — the frontend primer punts on `UmbracoCommunity.Extensions/Client/` and `UmbracoCommunity.BlockRestrictions/Client/`. A backoffice primer would cover both: App_Plugins manifest format, the Umbraco backoffice design system (`@umbraco-cms/backoffice`), how property editors / workspace views / dashboards are registered, and the dual-Vite-project setup.

## The full list

- ~~**`backend`**~~ — shipped (see top picks).
- **`multi-tenancy`** — see top picks. Pulls together an existing tutorial suite.
- **`backoffice`** — see top picks. Covers the two backoffice client codebases.
- **`content-modelling`** — document types, element types, block types, compositions (`ICompositionPageConfiguration`, `ICompositionSeo`, …), the auto-generated `PublishedModels` namespace, and the view-model-builder pipeline that converts `IPublishedContent` into view-shaped models. Threads together the `Models/` folder structure and the `BUILDING_PAGES.md` / `BUILDING_BLOCKS.md` how-tos.
- **`caching`** — caching is everywhere in this codebase but scattered: `AppCaches.RuntimeCache` (SVG TagHelper), `MemoryCache` (Vite manifest), `OutputCachePolicies` (API endpoints), `RequestCache` (per-request memoisation), `IsolatedCaches`. A primer would map which cache to reach for in which situation and what invalidates each.
- **`seo-and-structured-data`** — Schema.NET, the schema builders (`ArticleSchemaBuilder`, `OrganizationSchemaBuilder`, `BreadcrumbSchemaBuilder`), the `MetaTags` ViewComponent, OpenGraph and Twitter Cards in `Layout.cshtml`, sitemap generation, canonical URL handling. Cross-references the [tenant-aware schema fallback tutorial](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md).
- **`integrations`** — Sessionize, GitHub release tracking, Google Maps, Matomo, Intercom, Cookiebot. Each has its own configuration shape, its own API client pattern, and its own dev-mode story. A primer would inventory them and link to their feature folders.
- **`deployment`** — Umbraco Cloud deploys, the `copy-for-cloud.js` step, the staging-reset flow, the model-builder regen step. Probably overlaps heavily with `LESSONS_LEARNED.md` — primer would be the orientation layer, that doc remains the operational notes.

## How to use this list

When you write a primer:

1. Move its entry from this file into the "What's here" section in [`README.md`](./README.md) as part of the same commit that adds the primer.
2. Either delete the entry from this file or replace it with a one-line note that it shipped.
3. If the primer reveals a tutorial-shaped gap (something it wants to link to but no tutorial exists), add an entry to [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md) in the same commit.

The brainstorm shouldn't get stale. If an entry has been sitting here for six months and you've never reached for it, that's a signal to delete rather than to feel guilty about it.

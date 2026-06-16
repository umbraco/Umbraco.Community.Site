# Primer ideas

A backlog of candidate primers for future areas of the codebase. Each entry frames what the primer would orient readers to and what threads it would tie together. Prune entries that stop seeming worth writing; add new ones as you hit them.

These aren't tutorials — see [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md) for those. Primers are concept-oriented overviews; tutorials are deep dives. A primer should know what tutorials exist and link to them.

## Top picks

If you only write three more, these are the ones with the strongest "broad area with scattered docs that need threading" shape:

- ~~**`backend`**~~ — shipped as [backend.md](./backend.md).
- ~~**`multi-tenancy`**~~ — shipped as [multi-tenancy.md](./multi-tenancy.md).
- ~~**`backoffice`**~~ — shipped as [backoffice.md](./backoffice.md).

## The full list

- ~~**`backend`**~~ — shipped (see top picks).
- ~~**`multi-tenancy`**~~ — shipped as [multi-tenancy.md](./multi-tenancy.md). Pulls together an existing tutorial suite.
- ~~**`backoffice`**~~ — shipped as [backoffice.md](./backoffice.md). Covers the backoffice client codebases and why each is its own Vite project.
- **`content-modelling`** ([stub →](./content-modelling.md)) — document types, element types, block types, compositions (`ICompositionPageConfiguration`, `ICompositionSeo`, …), the auto-generated `PublishedModels` namespace, and the view-model-builder pipeline that converts `IPublishedContent` into view-shaped models. Threads together the `Models/` folder structure and the `BUILDING_PAGES.md` / `BUILDING_BLOCKS.md` how-tos.
- **`caching`** ([stub →](./caching.md)) — caching is everywhere in this codebase but scattered: `AppCaches.RuntimeCache` (SVG TagHelper), `MemoryCache` (Vite manifest), `OutputCachePolicies` (API endpoints), `RequestCache` (per-request memoisation), `IsolatedCaches`. A primer would map which cache to reach for in which situation and what invalidates each.
- **`seo-and-structured-data`** ([stub →](./seo-and-structured-data.md)) — Schema.NET, the schema builders (`ArticleSchemaBuilder`, `OrganizationSchemaBuilder`, `BreadcrumbSchemaBuilder`), the `MetaTags` ViewComponent, OpenGraph and Twitter Cards in `Layout.cshtml`, sitemap generation, canonical URL handling. Cross-references the [tenant-aware schema fallback tutorial](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md).
- **`integrations`** ([stub →](./integrations.md)) — Sessionize, GitHub release tracking, Google Maps, Matomo, Intercom, Cookiebot. Each has its own configuration shape, its own API client pattern, and its own dev-mode story. A primer would inventory them and link to their feature folders.

## How to use this list

Every idea above has a placeholder file linked inline (the `[stub →]` link next to the slug). When you write a primer:

1. Open the stub file and replace its scaffolding with the full primer — same path, same filename, no new file to create.
2. Move the bullet from here to the "What's here" section in [`README.md`](./README.md) in the same commit; either strike it through with a `— shipped as [path](path)` note or remove it outright.
3. If the primer reveals a tutorial-shaped gap (something it wants to link to but no tutorial exists), add an entry to [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md) — and a fresh stub file alongside it — in the same commit.

The brainstorm shouldn't get stale. If an entry has been sitting here for six months and you've never reached for it, that's a signal to delete both the bullet and its stub rather than to feel guilty about it.

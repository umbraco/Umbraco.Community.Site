---
tags: [primer, multi-tenancy, architecture, content-resolution]
---

# Multi-tenancy primer

This site runs **several distinct sites from one Umbraco instance** — each site is a *tenant*, with its own root content node, its own domain, its own settings and content, but a shared media library, shared user accounts, and a single deployment. That arrangement is cheap and convenient, and it comes with exactly one rule you have to keep in your head for nearly everything you write. This primer gives you the five-minute orientation; the [tutorial suite](#where-to-go-next) it links to has the depth.

> In a hurry? Skip to [the one rule](#the-navigation-rule). If you internalise nothing else, internalise that.

## How it's set up

Umbraco happily lets you keep multiple top-level content nodes in a single instance, each bound to its own domain (Settings → Culture and Hostnames on the root node). The backoffice tree ends up looking like this:

```
Content (Umbraco's "root")
├── Site A   (root content for community.example.com)
│   ├── Settings
│   │   ├── Navigation Settings
│   │   └── Social Settings
│   ├── About
│   ├── Blog
│   └── 404
├── Site B   (root content for events.example.com)
│   ├── Settings
│   │   ├── …
│   └── …
└── Site C   …
```

Every tenant has the same *shape* — a `Settings` node with children for navigation and social/SEO config, then content pages — but the data is per-tenant. When a request arrives for `community.example.com`, Umbraco's domain binding routes it into Site A's subtree, and your code's job is to stay inside that subtree.

## The navigation rule

> **Every content lookup starts from the current request's content node and walks up or down its own subtree. Nothing reaches across to a sibling root.**

Forget it and you get the signature multi-tenant bug: Site A's logo, navigation, or "Site Name" leaking onto Site B, because some lookup grabbed "the first root" or a hardcoded path instead of *this request's* tenant. It compiles, it passes your tests on the default tenant, and it ships broken to the second one. The helpers below exist to make doing the right thing the easy thing.

Two anti-patterns to recognise on sight: `umbracoHelper.ContentAtRoot().First()` (there are *several* roots — `.First()` picks whichever sorts first) and absolute content paths like `Content("/site-a/settings/social")` (breaks the moment a node is renamed or the site is forked).

## Resolving tenant content

Umbraco hands your controllers and view-model builders a `currentPage`, and from any content node three tools cover almost everything:

- **`currentPage.Root()`** — Umbraco's built-in walk up the ancestor chain to the topmost node, i.e. the tenant root.
- **`currentPage.GetSiteSettings()`** — the lever the rest of the codebase pulls. It walks to the tenant root, finds the `Settings` child, and returns it strongly-typed. `GetNavigationSettings()` and `GetSocialSettings()` chain off that same `Settings` node.
- **`currentPage.AncestorOrSelf<T>()`** — for "find the nearest containing X" (e.g. an article finding its parent `Blog`). Because the walk can't escape the tenant's subtree, tenant-safety comes for free.

Consumers — the menu, footer, SEO meta tags, sitemap — just call these and forget tenancy exists. Direct `Root()` calls are deliberately rare (essentially `GetSiteSettings()`, `BlogService.GetBlogPage()`, and the sitemap).

→ The [multi-tenant content resolution tutorial](../tutorials/foundations/multi-tenant-content-resolution.md) walks through all of this in code, including the small `settingsNode` optimisation that avoids resolving the root twice per render.

## When there's no current page

At the routing level you sometimes don't *have* a `currentPage` to anchor off — most notably a 404, where nothing matched the URL. Umbraco still knows which tenant the request belongs to, because it knows which domain was hit: **`request.Domain?.ContentId`** gives you the bound root node's id. That's the tenancy signal to resolve the tenant from the top down instead of the current page up.

→ The [per-tenant 404 refinement](../tutorials/refinements/per-tenant-404-content-finder.md) builds exactly this with a custom `INotFoundPageResolver` (via the NotFoundTracker package).

## Fail-soft when config is missing

Per-tenant brand data (organisation name, URL, logo) is editor-configurable. Rather than throwing an error on a freshly-set-up tenant, the SEO layer treats those settings as nullable all the way through and supplies a sensible hardcoded default at the bottom of the chain, so every page still emits valid `Organization` schema (the structured data search engines read to attribute a page to a publisher).

→ The [tenant-aware schema/SEO fallback refinement](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md) covers the fallback chain and why "all or nothing" beats partial fallbacks.

## Where the boundary is crossed on purpose

Two places legitimately reach across tenants, and they're not a smell:

- **The blog RSS middleware** — a URL-driven endpoint with no current page, so it enumerates roots (`ContentAtRoot()`) to find the matching `Blog` anywhere in the tree. It's *meant* to be tenant-agnostic.
- **The 404 fallback** — when there's no bound domain (local dev on `localhost`, a stray IP hit), the finder falls back to the first `PageNotFound` across all roots so *something* renders.

If you find yourself writing a *third* cross-tenant lookup, that's worth a second look — but the rule is "scope to the tenant unless you have a reason like these", not "never cross".

## The load-bearing assumption

The whole pattern rests on **document-type structure being uniform**: every tenant root has a `Settings` child, with consistently-named child types (`NavigationSettings`, `SocialSettings`, …). Renaming one of those aliases breaks every consumer, and adding a new tenant means remembering to create its `Settings` subtree. There's no compile-time enforcement — it's a convention held up by a small helper surface and code review.

## Where to go next

- **[Resolving content in a multi-tenant Umbraco site](../tutorials/foundations/multi-tenant-content-resolution.md)** — the foundation: the `Root()` + `GetSiteSettings()` helpers in code, the document-tree assumptions, and the consumer pattern.
- **[Per-tenant 404 pages with a custom `INotFoundPageResolver`](../tutorials/refinements/per-tenant-404-content-finder.md)** — tenancy resolution when there's no current page to start from.
- **[Tenant-aware fallback for schema and SEO metadata](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md)** — emitting valid structured data when a tenant hasn't filled in its brand fields.
- **[Backend primer](backend.md)** — where multi-tenancy sits in the wider request flow ("Multi-tenancy is the air").

Hold the one rule in your head and the rest follows — welcome aboard!

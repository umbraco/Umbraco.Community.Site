---
tags: [multi-tenant, content-resolution, site-settings, ipublishedcontent]
---

# Resolving content in a multi-tenant Umbraco site

This tutorial walks through the pattern the Umbraco Community site uses for **multi-tenancy** — running several distinct sites out of one Umbraco instance — and the small set of helpers that keep every content lookup scoped to *this request's* tenant rather than accidentally reaching into another one.

It's a *foundation* piece. Several other tutorials in this suite (the per-tenant 404 finder, the tenant-aware SEO schema fallback) build on top of it. If your project only ever serves one site from one Umbraco instance, you can skip this — `umbracoHelper.ContentAtRoot().First()` is fine for you. The pattern below only earns its keep once you have two or more tenant roots in the same tree.

## Why you might want this

Umbraco supports multiple top-level content nodes in a single instance, each bound to its own domain (or set of domains). That's the cheap way to host several sites from one CMS — shared media library, shared user accounts, shared deployments, but each site gets its own URLs, its own settings, its own content. The Umbraco backoffice looks something like this:

```
Content (Umbraco's "root")
├── Site A (root content for community.example.com)
│   ├── Settings
│   │   ├── Navigation Settings
│   │   └── Social Settings
│   ├── Home
│   ├── Blog
│   └── 404
├── Site B (root content for events.example.com)
│   ├── Settings
│   │   ├── Navigation Settings
│   │   └── Social Settings
│   ├── Home
│   └── 404
└── Site C (root content for ...)
    └── ...
```

Each tenant has the same *shape* — a Settings node with child nodes for navigation and social config, then content pages — but the actual data is per-tenant. When a request comes in for `community.example.com`, Umbraco's domain-binding code routes the request into Site A's subtree. Your job is to make sure every piece of view-model code respects that subtree boundary: when the header looks up the logo, it should find Site A's logo, not Site B's.

## What we're building

A pattern, not a single class. The headline rule is:

> **Every content lookup starts from the current request's content node and walks up or down its own subtree. Nothing reaches across to a sibling root.**

In code, that means three things:

1. **`currentPage.Root()`** is the lever for "give me the top of the current tenant's subtree". The `Root()` extension is built into Umbraco — it walks up the ancestor chain to the topmost content node.
2. **A small set of `Get<Setting>()` extension methods** wrap "from the current page, find this tenant's Settings node, then the configured child of that". They're the *only* place `Root()` gets called in this repo (with one exception we'll cover at the end).
3. **Document type structure mirrors the multi-tenant shape.** Every tenant has a `Settings` child of the root, with consistently-named child types (`NavigationSettings`, `SocialSettings`, …) underneath. The pattern only works because the *shape* is uniform; if Site B keeps its navigation as a property on the root node instead, every consumer needs special-cased lookup logic.

Once that's in place, view models, view components, and middleware all just call `currentPage.GetSiteSettings()` (or one of its siblings) and forget that tenancy exists.

## Why the obvious fix doesn't work

A handful of approaches that *seem* right but bite when you have more than one tenant:

**Absolute content paths.** Anything that looks like `umbracoHelper.Content("/site-a/settings/social")` works once and breaks every other time the site is forked. Even within a single tenant, content paths change when an editor renames a node. Don't hardcode them.

**`ContentAtRoot().First()`.** Multi-tenant content trees have *several* roots. `.First()` returns whichever Umbraco enumerates first, which depends on sort order in the backoffice. That can be the wrong tenant nine times out of ten and the right one during your test for the wrong reason.

**Domain-based lookup everywhere.** Umbraco knows which root is bound to which domain (`request.Domain?.ContentId`), and you can ask it. That's useful — the 404 content finder uses it precisely because the current page doesn't exist yet (see the [per-tenant 404 refinement](../refinements/per-tenant-404-content-finder.md)). But inside view-model code, you already have a `currentPage` from Umbraco's request pipeline, and it's already in the right tenant. Reaching for domain lookups when `Root()` would do the same job adds a service dependency and a service-locator-y feel for no win.

**Reading `IGlobalSettings` or `appsettings.json`.** Those give you Umbraco-instance defaults, not tenant brand. If Site A is the Umbraco Community and Site B is a Sessionize-driven events microsite, you don't want both to share the same "Site Name" from a config file.

## Walkthrough

The actual code is small. Three extension methods, one service that uses them, a handful of view-model builders that consume them.

### Step 1 — The `Root()` call

`Root()` is an extension method Umbraco ships on `IPublishedContent`. It walks ancestors until it hits the topmost content node — the tenant root, in our terms. You don't have to implement it; you just have to use it.

For most of this codebase, the only direct `Root()` calls live inside three places:

- **`PublishedContentExtensions.GetSiteSettings()`** — the centralised helper described below.
- **`BlogService.GetBlogPage()`** — `currentPage.Root()?.DescendantsOrSelf<Blog>().FirstOrDefault()` to find the tenant's Blog page from any content node within that tenant.
- **`SitemapController.Index()`** — `context.PublishedRequest?.PublishedContent?.Root()` to get the tenant root, then hand it to the sitemap service which walks downward.

Everything else uses the wrapper extension methods or the `AncestorOrSelf<T>()` pattern (described below). Direct `Root()` calls are rare, deliberate, and read as "I need the tenant root for an unusual reason that isn't in `Get<Setting>()`".

### Step 2 — The `GetSiteSettings()` helper

The single most important method in this pattern is in [`src/UmbracoCommunity.Web/Extensions/PublishedContentExtensions.cs`](../../../src/UmbracoCommunity.Web/Extensions/PublishedContentExtensions.cs):

```csharp
public static Settings? GetSiteSettings(this IPublishedContent content)
{
    var root = content.Root();
    if (root == null)
    {
        return null;
    }

    IPublishedContent? settingsNode = root
        .Children(x => x.ContentType.Alias == Settings.ModelTypeAlias)?
        .FirstOrDefault();

    return settingsNode?.As<Settings>();
}
```

That's the whole thing. From any content node, walk up to the tenant root, then look for a child whose document-type alias matches `Settings.ModelTypeAlias`. Return it as a strongly-typed `Settings` model (or null if the editor hasn't created one yet).

The reason this is the lever the rest of the codebase pulls is that **`Settings` is the parent of everything tenant-configurable**. Navigation, social/SEO, and any future per-tenant config live as children of this node. Once you have `Settings`, you have the door to all of them.

### Step 3 — Chaining off `Settings`

`NavigationSettings` and `SocialSettings` are children of the `Settings` node. Their helpers chain off `GetSiteSettings()`:

```csharp
public static NavigationSettings? GetNavigationSettings(
    this IPublishedContent content,
    IPublishedContent? settingsNode = null)
{
    Settings? settingsRoot = null;
    if (settingsNode == null || settingsNode is not Settings)
    {
        settingsRoot = content.GetSiteSettings();
        if (settingsRoot == null)
        {
            return null;
        }
    }
    else
    {
        settingsRoot = settingsNode as Settings;
    }

    var navSettings = settingsRoot?
        .Children(x => x.ContentType.Alias == NavigationSettings.ModelTypeAlias)?
        .FirstOrDefault();

    return navSettings?.As<NavigationSettings>();
}
```

`GetSocialSettings(...)` follows the same shape.

The `settingsNode` parameter is the optimisation worth flagging. A typical view-model builder needs **both** the `Settings` (for top-level fields like `SiteName`) **and** the `NavigationSettings` (for the menu). The naive implementation would call `GetSiteSettings()` once for the site name, then call `GetNavigationSettings()` — which would internally call `GetSiteSettings()` again. Same root walk, same `Children(...)` filter, twice. Passing the already-resolved `Settings` node into `GetNavigationSettings(siteSettings)` skips the second resolution.

```csharp
// In MenuViewModelBuilder.cs
var siteSettings = currentPage.GetSiteSettings();
viewModel.SiteName = siteSettings?.SiteName;

// Pass the already-resolved Settings; skips the second Root() + Children() walk.
var navSettings = currentPage.GetNavigationSettings(siteSettings);
```

This is small. On a single request it doesn't matter. On every page render of every request it adds up enough to be worth keeping the parameter.

### Step 4 — `AncestorOrSelf<T>` for "find the nearest containing X"

Not every tenant-scoped lookup is "find the Settings node". A blog article needs to find its parent `Blog` page — used for breadcrumbs, RSS feed URL, the "back to blog" link. The right tool for that isn't `Root()`; it's `AncestorOrSelf<T>()`:

```csharp
// In ArticlePageViewModelBuilder.cs
var blogPage = currentPage.AncestorOrSelf<Blog>();
```

`AncestorOrSelf<Blog>()` walks up from the current page looking for the first ancestor (or itself) that is a `Blog`. Because the article is *inside* the tenant's subtree, the walk can't escape into another tenant — `Root()` is the highest it can go, and we'd find or fail to find a `Blog` before then. Tenant safety comes for free.

The same pattern handles blog folder redirects (year/month folders walking up to find the `Blog` parent). Anywhere you'd naturally phrase the lookup as "the nearest containing X", reach for `AncestorOrSelf<X>()` rather than `Root().DescendantsOrSelf<X>().FirstOrDefault()`. It's faster, it's clearer, and it doesn't risk picking a sibling-tenant `X` if the document tree has more than one of them.

### Step 5 — Consumer pattern

Every view-model builder in this codebase that touches tenant config looks roughly the same. Here's the menu builder, trimmed:

```csharp
// In MenuViewModelBuilder.cs
private MenuViewModel CreateViewModelAndPopulateTopLevelNavigation(IPublishedContent currentPage)
{
    MenuViewModel viewModel = new();

    var siteSettings = currentPage.GetSiteSettings();
    viewModel.SiteName = siteSettings?.SiteName;
    viewModel.Logo = siteSettings?.HeaderLogo;

    var navSettings = currentPage.GetNavigationSettings(siteSettings);
    if (navSettings?.HeaderNavigationItems == null || !navSettings.HeaderNavigationItems.Any())
    {
        return viewModel;
    }

    foreach (var navItem in navSettings.HeaderNavigationItems)
    {
        // …enumerate header links from the tenant's nav config…
    }

    return viewModel;
}
```

The footer builder is identical in shape, just reading different fields off `siteSettings` and `navSettings`. The SEO/meta-tags service ([`SeoDataService.cs`](../../../src/UmbracoCommunity.Web/Services/SeoDataService.cs)) does the same thing for `SocialSettings`.

If you write a new view-model builder and find yourself reaching for `umbracoHelper.ContentAtRoot()` or anything cross-tenant, stop and check whether `GetSiteSettings()` / `GetSocialSettings()` / `AncestorOrSelf<T>()` is what you actually want.

### Step 6 — Where the pattern intentionally breaks

Two places cross tenant boundaries on purpose:

**The blog RSS middleware** ([`BlogRssMiddleware.cs`](../../../src/UmbracoCommunity.Web/Middleware/BlogRssMiddleware.cs)). The feed endpoint receives a URL path and has to find the `Blog` page anywhere in the content tree that matches. There's no current page to anchor off, so it falls back to enumerating roots:

```csharp
var blogPage = publishedContentQuery.ContentAtRoot()
    .SelectMany(r => r.DescendantsOrSelf<Blog>())
    .FirstOrDefault(b => /* URL match */);
```

This is the rare legitimate use of `ContentAtRoot()`. The middleware is, by design, not tenant-scoped — it's a URL-driven endpoint that could be hitting any tenant's blog.

**The 404 content finder fallback** ([`PageNotFoundContentFinder.cs`](../../../src/UmbracoCommunity.Web/Routing/PageNotFoundContentFinder.cs)). When a request doesn't have a bound domain (local dev on `localhost`, or a stray IP-address hit), the finder falls back to the first `PageNotFound` it can find across all roots. This is covered in detail in the [per-tenant 404 finder refinement](../refinements/per-tenant-404-content-finder.md), which builds directly on this foundation.

If you find yourself writing a third place that crosses tenant boundaries, that's a smell worth a second look — but it isn't categorically wrong.

## Alternatives we considered

- **A single `GetTenantRoot(IPublishedContent)` helper, used everywhere instead of `Root()` directly.** We considered this and didn't ship it. Umbraco's `Root()` already does what we'd implement, and wrapping it in our own helper would just be a renaming exercise. The leverage point is `GetSiteSettings()`, not the root lookup — the root lookup is barely an abstraction.
- **A `TenantContext` ambient service injected via DI.** "Read the current tenant's settings from `_tenantContext.SiteSettings`" reads nicely but adds a service to every view-model builder's constructor and hides the actual content-tree walk behind another layer. Extension methods on `IPublishedContent` keep the code closer to Umbraco's own API and make it obvious where the tenant boundary comes from. We'd revisit this if the per-tenant data outgrew the content tree (e.g. tenant config in a separate database).
- **Separate Umbraco instances per tenant.** The "real" multi-tenancy. Eliminates this entire class of bugs at the cost of duplicating deployments, databases, and editor accounts. For a handful of sites with a shared editor team and a small ops budget, one instance with this resolution pattern is the cheaper trade-off.
- **Umbraco's [variants/segments](https://docs.umbraco.com/umbraco-cms/fundamentals/data/variants) feature.** Built-in for *language* variants but not really designed for *brand* variants. Two tenants with different navigation structure and different page types don't fit the variant model.

## Trade-offs and known limits

- **No compile-time enforcement of tenant boundaries.** A new contributor who writes `umbracoHelper.ContentAtRoot().First().Settings` will compile, pass tests on the default tenant, and ship a bug to the second tenant. The pattern is a convention, not a guarantee. The mitigation is code review and the very small surface area of helpers — when there are three legitimate ways to do a thing, an unusual fourth way stands out.
- **`Children(...)` enumerates eagerly.** Each `GetSiteSettings()` call walks the children of the tenant root looking for a `Settings`-aliased node. Cheap when each root has ten children, less cheap if the root has hundreds. If that becomes a hot path, the optimisation is to cache the resolution per-request — Umbraco's `AppCaches.RequestCache` is the right layer. We haven't needed to.
- **Fail-soft on missing config.** `GetSiteSettings()` returns null if the editor hasn't created a `Settings` node yet. Every consumer has to handle null. That's a deliberate choice — throwing would mean a tenant with a half-built backoffice 500s instead of rendering a degraded page — but it shifts the burden onto every caller. Use the [tenant-aware SEO fallback pattern](../refinements/tenant-fallback-for-schema-and-seo.md) when "no config" should produce sensible defaults rather than empty markup.
- **Document-type structure is load-bearing.** The whole pattern assumes `Settings` is a child of every tenant root, with a consistent alias. Renaming the document-type alias breaks every consumer. Adding a new tenant means remembering to create the `Settings` subtree under it. A backoffice "checklist" pattern (validating new roots have the expected children) would catch this earlier — we don't have one.

## Where to go next

Two refinements build directly on this foundation:

→ [Per-tenant 404 pages with `IContentLastChanceFinder`](../refinements/per-tenant-404-content-finder.md) — the tenancy resolution problem when there *is* no current page to anchor off, because the request 404'd.

→ [Tenant-aware fallback for schema and SEO metadata](../refinements/tenant-fallback-for-schema-and-seo.md) — what to do when the tenant root exists but the editor hasn't filled in `SocialSettings` yet, and you still need to emit valid `Organization` schema.

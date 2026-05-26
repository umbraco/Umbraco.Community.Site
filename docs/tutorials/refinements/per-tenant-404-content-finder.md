---
tags: [multi-tenant, 404, content-finder, routing]
---

# Per-tenant 404 pages with `IContentLastChanceFinder`

> **Prerequisites:** This refinement builds on [Resolving content in a multi-tenant Umbraco site](../foundations/multi-tenant-content-resolution.md). That tutorial covers the `Root()` + `GetSiteSettings()` resolution pattern this one extends to a corner of Umbraco where there *is no current page* — the request 404'd before it got that far.

Multi-tenant Umbraco sites need multi-tenant 404 pages — it goes to reason that if a request lands on Tenant A's domain and ends up at a missing URL, the user shouldn't suddenly be looking at Tenant B's header and footer just because we couldn't find the page they asked for. This tutorial walks through the small content finder that makes that work, and the subtlety that forces it to resolve the tenant from the domain binding rather than from the (non-existent) current page.

## The problem

Out of the box, Umbraco gives you one site-wide 404 page, configured via `umbracoSettings.config` or the `ContentSettings` options. That's fine for a single-tenant site. With multiple tenants in the same instance:

- A 404 on `community.example.com` should render the **Umbraco Community** chrome (logo, nav, footer links).
- A 404 on `events.example.com` should render the **events microsite** chrome.

Both 404s should be served with HTTP status 404 — not a 200 with the right-looking page, because crawlers and uptime monitors are reading the status code.

Out of the box, you also can't *find* the right 404 page by walking from the current request's `IPublishedContent`. The whole reason you're here is that nothing matched the request URL. There is no current page. The foundation pattern from the multi-tenant tutorial — `currentPage.GetSiteSettings()` and friends — has no `currentPage` to lean on.

## Why the obvious fix doesn't work

A few approaches that don't actually get you there:

**Configure the 404 in `umbracoSettings`.** One global path. No way to vary by tenant without writing custom code anyway — at which point you might as well do it properly.

**Per-tenant 404 pages with no plumbing.** You can absolutely add a `PageNotFound` content node under each tenant root in the backoffice. Umbraco will happily let you publish them. But without code teaching the router that those nodes are the 404 pages, the router will never serve them — it'll still fall through to the global setting (or to a generic Kestrel/IIS 404).

**A custom 404 handler in `Program.cs` (`app.UseStatusCodePagesWithReExecute("/404")`).** Lets ASP.NET intercept the 404, re-execute against `/404`, and render whatever lives there. The catch: `/404` is a URL path, not a tenant-scoped concept. You'd have to either give every tenant a content node at the literal `/404` path (fragile, breaks if anyone publishes content at the same path), or write tenant-detecting routing on top of it. By the time you've done that, you've reimplemented `IContentLastChanceFinder` — Umbraco's purpose-built extension point — badly.

**Front-end / CDN 404 routing.** Detect the 404 status in Cloudflare/Akamai/whatever, rewrite the response to a per-tenant URL, fetch and serve. Works, but the routing logic lives outside the application and outside source control. Useful as a *backstop* but a poor primary mechanism.

**The version that does work:** implement `IContentLastChanceFinder`, hook into Umbraco's existing 404 resolution path, and resolve the tenant root from the **domain binding** that Umbraco already has on the incoming request.

## Our approach

Umbraco's request pipeline calls a chain of `IContentFinder` implementations in turn, asking each of them to match a URL to an `IPublishedContent`. When all of them fail to find anything, Umbraco calls a single `IContentLastChanceFinder` — exactly one, registered via `builder.SetContentLastChanceFinder<T>()`. That last-chance finder is the hook for "I'm about to serve a 404; would you like to serve content instead?", and it's exactly the right place for our per-tenant logic to live.

Inside `TryFindContent` you get an `IPublishedRequestBuilder` — think of it as Umbraco's mutable wrapper around the in-flight request, with hooks for "use this published content", "set this response status", and so on. It exposes `request.Domain?.ContentId`, which is the content ID of the root node bound to the request's domain. That's the tenancy signal you need: even though there's no current page, Umbraco *does* know which tenant the request belongs to, because it knew which domain was hit.

From there, the implementation is the foundation pattern in reverse: instead of `currentPage.Root()` → walk down, you have the root directly → walk down. Look for a `PageNotFound`-typed descendant under that root, hand it back to the request builder, set the status code to 404, done.

A fallback handles the case where there's no bound domain (local dev, IP-address requests, tests against the raw Kestrel host) — fall back to the first `PageNotFound` anywhere in the tree. This is one of the intentional cross-tenant lookups the foundation tutorial calls out.

## Walkthrough

The complete file lives at [`src/UmbracoCommunity.Web/Routing/PageNotFoundContentFinder.cs`](../../../src/UmbracoCommunity.Web/Routing/PageNotFoundContentFinder.cs).

### Step 1 — Implement `IContentLastChanceFinder`

The interface has one method: `Task<bool> TryFindContent(IPublishedRequestBuilder request)`. Return `true` if you handled it; `false` if you didn't and Umbraco should keep looking (which, for a last-chance finder, means falling back to a hard 404).

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Routing;

public class PageNotFoundContentFinder : IContentLastChanceFinder
{
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PageNotFoundContentFinder(
        IUmbracoContextAccessor umbracoContextAccessor,
        IServiceScopeFactory serviceScopeFactory)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        // …
    }
}
```

Two dependencies, both injected:

- **`IUmbracoContextAccessor`** to reach the published content cache. The `UmbracoContext` is Umbraco's per-request handle on the published content tree and the request itself; the *accessor* is the small interface you inject when you want to read that handle from anywhere outside an Umbraco render controller. We need it here because the content finder runs before any controller does.
- **`IServiceScopeFactory`** for the fallback path that needs `IPublishedContentQuery`. We'll explain why this is `IServiceScopeFactory` rather than a direct `IPublishedContentQuery` injection in step 4.

### Step 2 — Read the tenant root from the request's domain

The tenant lookup is one line:

```csharp
var rootContentId = request.Domain?.ContentId;
```

When the request hit `community.example.com`, Umbraco matched the host to a configured domain in the backoffice (Settings → Culture and Hostnames on the tenant root). That match populates `request.Domain` with — among other things — the `ContentId` of the bound root node. If no domain matched, `request.Domain` is null and `rootContentId` is null.

The strongly-typed `Models.PublishedModels.PageNotFound` is the document type the editor uses for tenant 404 pages. Umbraco's Models Builder generated it from a content type with alias `pageNotFound` — Models Builder reads your document types out of the database at build time and emits a typed C# class per type, so we can lean on the compiler instead of stringly-typed property lookups. Putting one of these `PageNotFound` nodes under each tenant root and publishing it is the editor-facing half of this feature.

### Step 3 — Resolve the root and find the 404 page

If the domain gave us a root content ID, ask the published content cache for the actual node, then walk its descendants:

```csharp
Models.PublishedModels.PageNotFound? notFoundPage = null;

if (rootContentId is not null
    && _umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
{
    var rootNode = umbracoContext.Content?.GetById(rootContentId.Value);
    notFoundPage = rootNode?
        .DescendantsOrSelf<Models.PublishedModels.PageNotFound>()
        .FirstOrDefault();
}
```

`DescendantsOrSelf<PageNotFound>()` walks the *tenant's* subtree — exactly the same scoping rule as the foundation pattern, just entered from a different starting point. Because we resolved `rootNode` from the domain-bound `ContentId`, we know we're inside the right tenant's subtree before the walk begins.

`FirstOrDefault()` returns null if the editor hasn't created a `PageNotFound` page yet under this tenant. That's not an error — it just means we don't have a custom 404 to offer for this tenant, and we'll fall through to the next step.

### Step 4 — Fall back to a cross-tenant search

If `rootContentId` was null (no domain match) or the per-tenant lookup returned nothing, find *any* `PageNotFound` in the tree:

```csharp
if (notFoundPage is null)
{
    using var scope = _serviceScopeFactory.CreateScope();
    var contentQuery = scope.ServiceProvider.GetRequiredService<IPublishedContentQuery>();
    notFoundPage = contentQuery.ContentAtRoot()
        .SelectMany(r => r.DescendantsOrSelf<Models.PublishedModels.PageNotFound>())
        .FirstOrDefault();
}
```

Two things to flag.

**Why `IServiceScopeFactory` instead of constructor-injecting `IPublishedContentQuery`?** `IPublishedContentQuery` is registered as scoped, and `IContentLastChanceFinder` implementations are resolved early in the request pipeline — early enough that the scoped-service captive-dependency rules can bite if you inject scoped services directly. Creating a fresh scope per call sidesteps the lifecycle question entirely. If you've been bitten by "cannot consume scoped service from singleton" errors in Umbraco's routing layer before, this is the workaround.

**Why a fallback at all?** Local dev and CI. When you run the site on `https://localhost:44389` with no hostname configured in the backoffice, there's no bound domain and `request.Domain` is null. You still want a 404 page to render rather than a Kestrel error page. Same for tests that bypass hostname routing. In production with hostnames configured, the fallback should essentially never fire — if it does, you've got a tenant without a `PageNotFound` page, which is worth catching in a deployment checklist.

### Step 5 — Hand the page back to the router

If we found a page (either path), tell the request builder to use it and set the status code to 404 explicitly:

```csharp
if (notFoundPage is null)
{
    return Task.FromResult(false);
}

request.SetPublishedContent(notFoundPage);
request.SetResponseStatus(404);

return Task.FromResult(true);
```

`SetPublishedContent` makes Umbraco render the 404 page through its normal pipeline — your `Render` controllers, view models, layout, the lot. The response body looks identical to any other content page on the site, which is exactly what you want for the user. `SetResponseStatus(404)` keeps the HTTP status truthful for crawlers, uptime monitors, and analytics.

The full `TryFindContent` body, end to end:

```csharp
public Task<bool> TryFindContent(IPublishedRequestBuilder request)
{
    Models.PublishedModels.PageNotFound? notFoundPage = null;

    var rootContentId = request.Domain?.ContentId;

    if (rootContentId is not null
        && _umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
    {
        var rootNode = umbracoContext.Content?.GetById(rootContentId.Value);
        notFoundPage = rootNode?
            .DescendantsOrSelf<Models.PublishedModels.PageNotFound>()
            .FirstOrDefault();
    }

    if (notFoundPage is null)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var contentQuery = scope.ServiceProvider.GetRequiredService<IPublishedContentQuery>();
        notFoundPage = contentQuery.ContentAtRoot()
            .SelectMany(r => r.DescendantsOrSelf<Models.PublishedModels.PageNotFound>())
            .FirstOrDefault();
    }

    if (notFoundPage is null)
    {
        return Task.FromResult(false);
    }

    request.SetPublishedContent(notFoundPage);
    request.SetResponseStatus(404);

    return Task.FromResult(true);
}
```

### Step 6 — Register it

`IContentLastChanceFinder` is a *single-implementation* contract. Umbraco's `SetContentLastChanceFinder<T>()` registers your type and replaces whatever was there before. Wrap it in a composer in the same file — a *composer* is an Umbraco-specific startup hook: any class that implements `IComposer` is picked up at boot and given a chance to register things with the DI container, which is exactly what we need to tell Umbraco about our last-chance finder:

```csharp
public class PageNotFoundContentFinderComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.SetContentLastChanceFinder<PageNotFoundContentFinder>();
    }
}
```

This composer gets picked up by `.AddComposers()` in `Program.cs` automatically (it implements `IComposer` and lives in an assembly Umbraco scans). No explicit `Program.cs` registration needed.

The DI registration for `PageNotFoundContentFinder` itself is *also* done by `SetContentLastChanceFinder<T>()` — it adds the finder as a singleton in Umbraco's DI under the `IContentLastChanceFinder` contract. That's why you don't see a separate `AddScoped` / `AddSingleton` for the finder in this codebase's `UmbracoBuilderExtensions.cs`.

## Alternatives we considered

- **`app.UseStatusCodePagesWithReExecute("/404")`.** Mentioned above as an "obvious fix that doesn't work" — including here as the explicit comparison. The advantage is it's pure ASP.NET; you can use it without Umbraco-specific extension points. The disadvantage is everything that goes into making `/404` actually tenant-aware: per-tenant content paths, special-cased routing, status-code juggling between the re-execute and the response. By the time the plumbing's done, you've written a worse version of `IContentLastChanceFinder`.
- **A `RenderController` action that takes over for any unmatched route.** Works for one tenant. For multiple, you'd need to inspect `HttpContext.Request.Host` inside the action to figure out which tenant's 404 to serve, then resolve the content yourself. The content finder is purpose-built for this and integrates with Umbraco's pipeline (preview mode, members, render-time block previews, …) for free.
- **Static per-tenant 404 HTML files served via middleware.** Fast, no Umbraco involved, no risk of cascading exceptions on the 404 path. The trade-off is editors can't update the 404 page in the backoffice without a code deploy. For a site where editors *do* update the 404 (link to current event, seasonal copy, branded illustration), the dynamic version is worth the cost.
- **Multiple last-chance finders.** `SetContentLastChanceFinder<T>()` accepts only one. If you needed to chain logic — try a per-tenant 404, then a global one, then a "did you mean…" search — you'd build that chain inside your single `TryFindContent`. Not worth it for this site; if your requirements grow that way, consider extracting a small strategy pattern inside the finder.

## Trade-offs and known limits

- **Per-tenant `PageNotFound` is an editor responsibility.** If a tenant is set up without one, the fallback fires and that tenant's 404 will look like another tenant's. The right mitigation is a deployment checklist or a backoffice health check, not code — making it impossible to set up a tenant without a 404 would mean refusing to publish the tenant root, which is heavier than the problem warrants.
- **Domain binding is the only tenancy signal.** If you forget to configure hostnames on a new tenant's root in the backoffice, the request won't have a bound domain and the fallback will fire on every request. The symptom is "this site always shows Tenant A's 404 even though we're on Tenant B's domain". Check Settings → Culture and Hostnames first.
- **No caching.** Every 404 walks the tenant subtree looking for the `PageNotFound` node. A heavily-attacked endpoint (404-spamming bot) does this once per request. The walk is cheap (descendants of a tenant root are not many, and the published content cache is in-memory), but if you wanted to make it free, you could cache the resolved `PageNotFound` per `rootContentId` in `AppCaches.RuntimeCache` keyed by the root ID. We haven't, because 404s aren't a hot path in our traffic.
- **Status code is set on the request, not by the controller.** This works because Umbraco's pipeline reads `SetResponseStatus` and writes the corresponding HTTP status. If you have any *other* status-code-mutating middleware downstream (compression, error pages, custom auth challenges), make sure they read or preserve the 404 you set here — a downstream middleware that resets the status to 200 will silently break the contract with crawlers.
- **`IContentLastChanceFinder` is singleton-ish.** The composer registers one. If a NuGet package you install also calls `SetContentLastChanceFinder<T>()`, the *last* registration wins. If you have an upstream package with its own last-chance behaviour you want to compose with, you'll need to either fork or merge their logic into yours.

## Where to go next

The other refinement that builds on the multi-tenant resolution foundation handles a related question: what happens when the tenant root exists but the editor hasn't configured the brand metadata yet, and you still need to emit valid `Organization` schema in the page head?

→ [Tenant-aware fallback for schema and SEO metadata](./tenant-fallback-for-schema-and-seo.md)

Hopefully that takes the "but where do I hook the 404 in?" head-scratching off your plate the next time you set up a multi-tenant site.

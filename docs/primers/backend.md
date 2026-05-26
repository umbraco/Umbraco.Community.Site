---
tags: [primer, backend, architecture, controllers, view-models]
---

# Backend primer

The C# side of the site lives across five projects, with **`src/UmbracoCommunity.Web/`** as the heart of it all — controllers, view model builders, services, view components, and the small ASP.NET integrations Umbraco doesn't ship by default. The aim of this primer is to give you the lay of the land on the backend, as sibling to the [frontend primer](frontend.md). Each section sketches what's there and signposts a deeper doc for when you need it.

> Just looking for the folder map? Skip to [Where things live](#where-things-live).

## Solution shape

```
src/
├── UmbracoCommunity.Web.UI/             ← Host project (Program.cs, Views/, wwwroot/, appsettings)
├── UmbracoCommunity.Web/                ← Core C# (controllers, models, services, builders, ...)
├── UmbracoCommunity.StaticAssets/       ← Vite-built frontend assets — see frontend primer
├── UmbracoCommunity.Extensions/         ← Umbraco backoffice extensions (RCL + own client)
└── UmbracoCommunity.BlockRestrictions/  ← Block restrictions backoffice + EF Core migrations
```

`UmbracoCommunity.Web.UI` is the runnable project — it owns `Program.cs`, the Razor views, `wwwroot/`, and `appsettings.json`. `UmbracoCommunity.Web` is everything else C#: controllers, models, services, builders, middleware. The other three are: a Vite asset bundle that gets copied in (see the [frontend primer](frontend.md)), and two backoffice extension RCLs that ship their own backoffice clients (covered in the future backoffice primer — entry in [`IDEAS.md`](IDEAS.md)).

Package versions are managed centrally via `Directory.Packages.props`.

## Request flow

Before getting into the folder-by-folder shape, it's worth following a request through the system once — most of the rest of the primer makes more sense once you can see the path. A request that resolves to an Umbraco content node travels through this:

```
HTTP request
  ↓
Umbraco routing  (matches URL → IPublishedContent)
  ↓
RenderController  (Controllers/Render/) — route-hijacking by content type
  ↓
IViewModelBuilder<T>  (ViewModelBuilders/Pages/) — IPublishedContent → ViewModel
  ↓
CurrentTemplate(viewModel) → Razor view (Views/) renders
  ↓
ViewComponents  (MetaTags, Menu, Footer, Favicon) fill layout slots
```

Each step has a sharp responsibility. Controllers are thin — they receive the request, hand off to a builder, return the result. Builders convert `IPublishedContent` (Umbraco's polymorphic content type) into a strongly-typed view model. Views render the view model. View components fill cross-cutting layout slots (meta tags, menu, footer) so the page-level builder doesn't have to compose them.

API endpoints (`Controllers/Api/`) skip the view-model and Razor steps entirely and return JSON directly via `[ApiController]`.

## Multi-tenancy is the air

Let us take a moment to talk about something that affects nearly everything you'll write here. This site runs multiple tenants from one Umbraco instance — each tenant has its own root content node and its own content tree underneath. The single invariant to hold in your head is this: **every content lookup must be scoped to the current request's tenant**. Never assume a single root, never use hardcoded content paths. The standard helpers (`currentPage.Root()`, `GetSiteSettings()`, the domain-binding pattern in custom content finders) all exist to make that scoping cheap. Forget it, and tenant A's navigation will end up leaking into tenant B before you know what's happened.

If this is the first content-related code you're writing for this codebase, do take the time to read through the [multi-tenant content resolution tutorial](../tutorials/foundations/multi-tenant-content-resolution.md) before going much further — it's a small investment that will save you a class of bug.

## Three controller flavours

`Controllers/` is organised by controller kind:

| Folder | Base class | Used for | Example |
| --- | --- | --- | --- |
| `Controllers/Render/` | `RenderController` | Route-hijacking — taking over the render path for a specific content type | [`HomeController`](../../src/UmbracoCommunity.Web/Controllers/Render/HomeController.cs), `BlogController`, `ArticleController` |
| `Controllers/Api/` | `ControllerBase` + `[ApiController]` | JSON API endpoints called from frontend code | [`BlogApiController`](../../src/UmbracoCommunity.Web/Controllers/Api/BlogApiController.cs), `SeedController` |
| `Controllers/` (root) | `Controller` | Plain MVC endpoints unrelated to Umbraco content — usually small (robots.txt, security.txt) | `RobotsController`, `SecurityTxtController` |

Render controllers are named to match their document type alias — `HomeController` handles the `Home` content type. Umbraco's routing scans for `RenderController`-derived classes by name and hands off automatically; you don't wire them up explicitly, which is rather nice.

A canonical render controller is small — here is the shape we follow:

```csharp
public class HomeController : RenderController
{
    private readonly IViewModelBuilder<HomePageViewModel> _viewModelBuilder;

    public HomeController(
        ILogger<HomeController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IViewModelBuilder<HomePageViewModel> viewModelBuilder)
        : base(logger, compositeViewEngine, umbracoContextAccessor) =>
        _viewModelBuilder = viewModelBuilder;

    public IActionResult Index(CancellationToken cancellationToken)
    {
        HomePageViewModel viewModel = _viewModelBuilder.Build(
            CurrentPage ?? throw new InvalidOperationException(...),
            UmbracoContext);
        return CurrentTemplate(viewModel);
    }
}
```

That really is the whole pattern. New page types are a controller, a builder, and a view model — see [`docs/BUILDING_PAGES.md`](../BUILDING_PAGES.md) for the step-by-step when you come to add one.

## The view model builder pattern

If you take one thing from this section, take this: the builder is where `IPublishedContent` (Umbraco's polymorphic content type) gets turned into a strongly-typed view model your Razor view can use without ceremony. Builders implement [`IViewModelBuilder<TViewModel>`](../../src/UmbracoCommunity.Web/ViewModelBuilders/IViewModelBuilder.cs):

```csharp
public interface IViewModelBuilder<out TViewModel>
{
    TViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext);
}
```

There are two siblings: `IViewModelBuilder<TInputModel, TViewModel>` (when the builder needs an extra input alongside the content node), and an async variant in [`IAsyncViewModelBuilder.cs`](../../src/UmbracoCommunity.Web/ViewModelBuilders/IAsyncViewModelBuilder.cs).

Builders are registered as scoped services (one instance per request) in [`UmbracoBuilderExtensions.AddViewModelBuildersAndDecorators()`](../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs). Controllers inject the *interface* and call `.Build(...)` — they don't see the concrete class. This costs ~one line of DI registration per builder and buys testability.

Organised by purpose:

- **`ViewModelBuilders/Pages/`** — one builder per page-level content type (Home, Blog, Article, ContentPage).
- **`ViewModelBuilders/Components/`** — builders for cross-cutting components (Menu, Footer).
- **`ViewModelBuilders/Schema/`** — schema.org builders (`ArticleSchemaBuilder`, `BreadcrumbSchemaBuilder`, `OrganizationSchemaBuilder`). Covered in the [tenant-aware schema fallback tutorial](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md).

A shared [`ViewModelBuilderBase`](../../src/UmbracoCommunity.Web/ViewModelBuilders/ViewModelBuilderBase.cs) hosts helpers most builders need — image URL resolution with WebP conversion, the `GetWebPageName(ICompositionSeo)` shortcut. New builders inherit it.

## Bootstrapping

[`Program.cs`](../../src/UmbracoCommunity.Web.UI/Program.cs) is small. The interesting bit is the Umbraco builder chain:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddOutputCaching()                  // Extensions/UmbracoBuilderExtensions.cs
    .AddSecurityPolicies()               // CSP + HSTS
    .AddViewModelBuildersAndDecorators()
    .AddPipelineFilters()                // Extensions/WebApplicationExtensions.cs
    .Build();
```

A **composer** is an Umbraco-specific startup hook: implement `IComposer`, override `Compose(IUmbracoBuilder)`, and Umbraco calls it during boot. `AddComposers()` auto-discovers any `IComposer` in the assembly.

When to write a composer vs. add to the chained `AddX` above: use a composer when the registrations belong to a single Feature or notification handler — it keeps the wiring next to the code it serves, and the rest of the codebase doesn't have to know that bit of DI exists. Use the chained `AddX` when the registration is cross-cutting and shared across the whole site (view-model builders, security policies, output caching).

Three current composer examples:

- [`Features/Sessionize/Configuration/RegisterSessionize.cs`](../../src/UmbracoCommunity.Web/Features/Sessionize/Configuration/RegisterSessionize.cs) wires up the Sessionize API client, options, and a named `HttpClient`.
- [`Notifications/CacheNotificationsComposer.cs`](../../src/UmbracoCommunity.Web/Notifications/CacheNotificationsComposer.cs) registers Umbraco content notification handlers.
- The 404 content finder registers itself via its own composer — see the [per-tenant 404 tutorial](../tutorials/refinements/per-tenant-404-content-finder.md).

The chained `AddX` methods live in [`Extensions/UmbracoBuilderExtensions.cs`](../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs) and [`Extensions/WebApplicationExtensions.cs`](../../src/UmbracoCommunity.Web/Extensions/WebApplicationExtensions.cs). They keep `Program.cs` readable by pushing the actual registrations into named groups.

## Day-to-day

- **Local secrets** — `appsettings.Local.json` is gitignored; put API keys, connection strings, and anything else you don't want committed there. It overlays `appsettings.json` automatically when present.
- **Debugging** — attach your debugger to the running `dotnet run` process from `src/UmbracoCommunity.Web.UI`. Render-controller breakpoints fire on each matching page request; view-component breakpoints fire on layout composition. View files (`.cshtml`) compile per-request, so you can edit a view and hit refresh without rebuilding.
- **Adding a NuGet package** — package versions are pinned centrally in [`Directory.Packages.props`](../../Directory.Packages.props) (central package management). Add a `<PackageVersion Include="X" Version="…" />` there first, then a bare `<PackageReference Include="X" />` in the project's `.csproj` (no version on the reference itself).

## Where things live

Under `UmbracoCommunity.Web/`:

| Folder | What's in it |
| --- | --- |
| `Controllers/` | The three flavours described above — `Render/`, `Api/`, root |
| `Models/` | View models, content models, DTOs. Subfolders: `Api/`, `Pages/`, `ContentModels/`, `ViewModels/{Blocks,Components,Properties}/`, `ServiceModels/`, `PublishedModels/` *(auto-generated — don't edit)* |
| `ViewModelBuilders/` | The builder pipeline. Subfolders: `Pages/`, `Components/`, `Schema/` |
| `ViewComponents/` | Layout-slot components (`MetaTagsViewComponent`, `MenuViewComponent`, `FooterViewComponent`, `FaviconViewComponent`) |
| `Services/` | Reusable services (`ContentDataService`, `ContentContextService`, `SeoDataService`, `BlogService`) |
| `Features/` | Self-contained feature modules — currently just `Sessionize/` |
| `Routing/` | Custom content finders (`PageNotFoundContentFinder`) |
| `Middleware/` | Custom ASP.NET middleware (CSP disable, blog folder redirects, form validation) |
| `Notifications/` | Umbraco notification handlers (cache invalidation tied to content events) |
| `Extensions/` | `IUmbracoBuilder` + `IServiceCollection` extension methods, including the named-policy `OutputCachePolicies` static class |
| `TagHelpers/` | Custom Razor TagHelpers (`SvgTagHelper`, `NonceTagHelper`) — see the [inline SVG tutorial](../tutorials/foundations/inline-svg-tag-helper.md) |
| `Vite/` | The Razor↔Vite manifest bridge — see the [frontend primer](frontend.md) |
| `Utilities/`, `Helpers/` | Helper classes; utilities are pure (string, URL, semver), helpers wrap domain concerns (colour, image, video) |
| `Configuration/`, `Constants.*.cs` | Options classes and string constants |

## ViewComponents for layout slots

Layout concerns — meta tags, menu, footer, favicon — are handled by [ViewComponents](../../src/UmbracoCommunity.Web/ViewComponents/), invoked from `Views/Shared/_Layout.cshtml`. A view component is an ASP.NET pattern that works like a mini-controller scoped to rendering one slice of layout: it has its own DI scope, pulls its own data, and renders its own partial view.

Use a view component when:

- The data needed by a layout slot is unrelated to whatever page-level data the controller is building (menu, footer, meta tags all fall into this bucket).
- A layout slot needs to be opt-in per page — `/error.html` simply doesn't invoke `@await Component.InvokeAsync("Menu")` and the menu is skipped.
- You want the slot's data assembly testable in isolation, without spinning up a whole controller.

If you've seen action-filter patterns that stash layout data on `ViewData` in older ASP.NET codebases, prefer view components — they keep the layout concerns out of the controller entirely.

## Output caching

Caching is the kind of thing that gets layered on as a project grows, so it's worth showing where it lives before you need to find it. API endpoints decorate with `[OutputCache(PolicyName = ...)]`; the policies themselves are defined in [`Extensions/UmbracoBuilderExtensions.cs`](../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs):

- **`OutputCachePolicies.ContentDriven`** — long expiration, tagged so notification handlers (`Notifications/`) can evict on content publish/unpublish. (Cache tags are an ASP.NET output-caching feature: you stamp a tag like `"content:123"` on a cached entry, then later call `IOutputCacheStore.EvictByTagAsync("content:123")` to invalidate every entry sharing that tag.)
- **`OutputCachePolicies.ExternalApi`** — time-based expiration for data we don't control (Sessionize).

The pattern: cache durations are config-bound via `OutputCacheOptions` (settable per environment in `appsettings.*.json`), eviction is tag-based for content-driven endpoints, time-based for external integrations. New endpoint? Pick a policy and decorate.

→ A future caching primer would thread output caching together with `AppCaches.RuntimeCache`, `MemoryCache`, and `RequestCache` so the whole story sits in one place — see [`IDEAS.md`](IDEAS.md).

## Self-contained features

The `Features/` folder is the home for modules with enough internal structure (controllers, models, infrastructure, options) to be worth bundling. The current example is `Features/Sessionize/`:

```
Features/Sessionize/
├── Configuration/RegisterSessionize.cs    ← composer; wires the feature into DI
├── Controllers/SessionizeApiController.cs ← endpoints under /api/sessionize
├── Infrastructure/SessionizeApiClient.cs  ← cached service talking to Sessionize
├── Infrastructure/SessionizeOptions.cs    ← bound to appsettings "Sessionize" section
└── Models/Sessionize*.cs                  ← DTOs returned to the frontend
```

The composer is the entry point — `AddComposers()` finds it, and from there the feature wires itself up entirely. The rest of the codebase doesn't have to know `Features/Sessionize/` exists; it just sees `/api/sessionize/*` endpoints at runtime.

Promote from a flat folder to a `Features/<Name>/` module when there are 5+ classes that all belong together (controllers + service + options + DTOs).

## Notifications and middleware

- **Notifications** (`Notifications/`) handle Umbraco-emitted events. The current handler invalidates the output cache when blog content is published — see [`BlogContentCacheInvalidationHandler.cs`](../../src/UmbracoCommunity.Web/Notifications/BlogContentCacheInvalidationHandler.cs). Cache invalidations or other content-driven side effects go here.
- **Middleware** (`Middleware/`) handles request-pipeline concerns Umbraco doesn't own — CSP disable per request, blog folder redirects, form-validation defaults.

## Related how-to docs

- **[`docs/BUILDING_PAGES.md`](../BUILDING_PAGES.md)** — adding a new page type end to end (doc type → render controller → view model → builder → view).
- **[`docs/BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md)** — adding a new content block (element type → content model → view model → partial view).
- **[`CODE_CONVENTIONS.md`](../../CODE_CONVENTIONS.md)** — naming patterns (`{DocTypeAlias}Controller`, `{DocTypeAlias}PageViewModelBuilder`, etc.), controller-organisation rules, and the file/namespace conventions referenced throughout this primer.

For *why* specific patterns are shaped the way they are — multi-tenant content resolution, the SEO schema fallback, the custom content finder for 404s — see the [tutorials suite](../tutorials/README.md). Each tutorial stands alone, so you can dip in wherever a problem you're hitting matches.

Hopefully that gives you enough to find your way around — welcome aboard!

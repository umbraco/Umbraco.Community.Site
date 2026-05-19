# Backend primer

The C# side of the site lives across five projects, with **`src/UmbracoCommunity.Web/`** as the heart — controllers, view model builders, services, view components, and the small ASP.NET integrations Umbraco doesn't ship by default. This primer is an orientation doc, sibling to the [frontend primer](frontend.md); each section sketches what's there and links out for depth.

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

A request that resolves to an Umbraco content node travels through this path:

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

## Three controller flavours

`Controllers/` is organised by controller kind:

| Folder | Base class | Used for | Example |
| --- | --- | --- | --- |
| `Controllers/Render/` | `RenderController` | Route-hijacking — taking over the render path for a specific content type | [`HomeController`](../../src/UmbracoCommunity.Web/Controllers/Render/HomeController.cs), `BlogController`, `ArticleController` |
| `Controllers/Api/` | `ControllerBase` + `[ApiController]` | JSON API endpoints called from frontend code | [`BlogApiController`](../../src/UmbracoCommunity.Web/Controllers/Api/BlogApiController.cs), `SeedController` |
| `Controllers/` (root) | `Controller` | Plain MVC endpoints unrelated to Umbraco content — usually small (robots.txt, security.txt) | `RobotsController`, `SecurityTxtController` |

Render controllers are named to match their document type alias — `HomeController` handles the `Home` content type. Umbraco's routing scans for `RenderController`-derived classes by name and hands off automatically; you don't wire them up explicitly.

A canonical render controller is small:

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

That's the whole pattern. New page types are a controller, a builder, and a view model — see [`docs/BUILDING_PAGES.md`](../BUILDING_PAGES.md) for the step-by-step.

## The view model builder pattern

Builders implement [`IViewModelBuilder<TViewModel>`](../../src/UmbracoCommunity.Web/ViewModelBuilders/IViewModelBuilder.cs):

```csharp
public interface IViewModelBuilder<out TViewModel>
{
    TViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext);
}
```

There are two siblings: `IViewModelBuilder<TInputModel, TViewModel>` (when the builder needs an extra input alongside the content node), and an async variant in [`IAsyncViewModelBuilder.cs`](../../src/UmbracoCommunity.Web/ViewModelBuilders/IAsyncViewModelBuilder.cs).

Builders are registered as scoped services in [`UmbracoBuilderExtensions.AddViewModelBuildersAndDecorators()`](../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs). Controllers inject the *interface* and call `.Build(...)` — they don't see the concrete class. This costs ~one line of DI registration per builder and buys testability + space for decorators (the `AndDecorators` part of the method name is forward-looking; wrapping decorators would be added here when needed).

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

`AddComposers()` auto-discovers any `IComposer` in the assembly. Composers are the right home for *self-contained registrations* — things that belong to a Feature or a Notification handler rather than to the centralised `AddViewModelBuildersAndDecorators`. Three current examples:

- [`Features/Sessionize/Configuration/RegisterSessionize.cs`](../../src/UmbracoCommunity.Web/Features/Sessionize/Configuration/RegisterSessionize.cs) wires up the Sessionize API client, options, and a named `HttpClient`.
- [`Notifications/CacheNotificationsComposer.cs`](../../src/UmbracoCommunity.Web/Notifications/CacheNotificationsComposer.cs) registers Umbraco content notification handlers.
- The 404 content finder registers itself via its own composer — see the [per-tenant 404 tutorial](../tutorials/refinements/per-tenant-404-content-finder.md).

The chained `AddX` methods live in [`Extensions/UmbracoBuilderExtensions.cs`](../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs) and [`Extensions/WebApplicationExtensions.cs`](../../src/UmbracoCommunity.Web/Extensions/WebApplicationExtensions.cs). They keep `Program.cs` readable by pushing the actual registrations into named groups.

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

## ViewComponents over action filters

Layout concerns — meta tags, menu, footer, favicon — are handled by [ViewComponents](../../src/UmbracoCommunity.Web/ViewComponents/), invoked from `Views/Shared/_Layout.cshtml`. They pull their own data via injected services; the page-level builder doesn't have to compose them into its view model.

An earlier convention in similar codebases pushes layout data onto `ViewData` via action filters. View components are preferred here because:

- They're invokable from Razor (`@await Component.InvokeAsync("Menu")`), so layout slots can opt in or out — useful for `/error.html`-style pages that don't have a menu.
- They have their own DI scope and can build their data asynchronously without leaking into the controller.
- Tests can render a view component in isolation.

If you're tempted to write an action filter for a layout concern, write a view component instead.

## Output caching

API endpoints decorate with `[OutputCache(PolicyName = ...)]`. Policies are defined in [`Extensions/UmbracoBuilderExtensions.cs`](../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs):

- **`OutputCachePolicies.ContentDriven`** — long expiration, tagged so notification handlers (`Notifications/`) can evict on content publish/unpublish.
- **`OutputCachePolicies.ExternalApi`** — time-based expiration for data we don't control (Sessionize).

The pattern: cache durations are config-bound (`OutputCacheOptions`), eviction is tag-based for content-driven endpoints, time-based for external integrations. New endpoint? Pick a policy and decorate.

→ A future caching primer (entry in [`IDEAS.md`](IDEAS.md)) would thread output caching together with `AppCaches.RuntimeCache`, `MemoryCache`, and `RequestCache` so the whole story sits in one place.

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

For *why* specific patterns are shaped the way they are — multi-tenant content resolution, the SEO schema fallback, the custom content finder for 404s — see the [tutorials suite](../tutorials/README.md).

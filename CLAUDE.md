# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the Umbraco Community Website - a replacement for [community.umbraco.com](https://community.umbraco.com). It's an ASP.NET Core application built on Umbraco CMS with a Vite-powered frontend, featuring automated GitHub integration for release tracking, community data synchronization, and Sessionize event integration.

## Solution Structure

The solution consists of 4 projects (uses Central Package Management via `Directory.Packages.props`):

- **UmbracoCommunity.Web.UI** - Main web application (startup project)
- **UmbracoCommunity.Web** - Core business logic, features, controllers, view models
- **UmbracoCommunity.StaticAssets** - Frontend assets built with Vite (TypeScript, Lit web components)
- **UmbracoCommunity.Extensions** - Umbraco backoffice extensions (Razor Class Library with TypeScript client in `Client/` folder)

### Key Directories in UmbracoCommunity.Web

- **Features/** - Feature modules (GitHubSync, ReleaseOverview, Sessionize)
- **Controllers/** - MVC controllers organized by type:
  - `Controllers/Api/` - API controllers (inherit from `ControllerBase`, have `[ApiController]` attribute)
  - `Controllers/Render/` - Umbraco render controllers (inherit from `RenderController`)
  - Root folder - Plain MVC controllers (inherit from `Controller`)
- **Models/** - View models, blocks, and published content models
  - `Models/Api/` - DTOs for API request/response objects
  - `Models/Pages/` - Page view models
  - `Models/Blocks/` - Block view models
  - `Models/ViewModels/Components/` - Reusable component view models
- **ViewModelBuilders/** - Convert IPublishedContent to view models
- **Services/** - Application services (`ContentDataService`)
- **Extensions/** - Extension methods for ASP.NET Core builders, Umbraco helpers, CSP, and HTML helpers
- **Attributes/** - Action filters (`ApplyCommonElements`, `ApplyPageMetaData`)
- **Middleware/** - Custom middleware (CSP handling)
- **Utilities/** - Helper classes (`ReleaseDiscussionParser`, `ReleaseLabelHelper`, `SemVerHelper`, `StringUtilities`)
- **Helpers/** - Domain helpers (ColourHelper, ImageHelper, VideoHelper)
- **Migrations/** - EF Core migrations for GitHubDbContext
- **TagHelpers/** - Custom tag helpers (SvgTagHelper, NonceTagHelper)
- **Vite/** - Vite integration helpers and tag helpers

## Development Setup

### Running the Application

The development environment requires two processes running simultaneously:

1. **Backend (Umbraco CMS)**:
   ```bash
   cd src/UmbracoCommunity.Web.UI
   dotnet run
   ```

2. **Frontend (Vite dev server)** - in a separate terminal:
   ```bash
   cd src/UmbracoCommunity.StaticAssets
   npm run dev
   ```
   If missing components: `npm ci`

The Vite dev server runs on port 5123 and provides HMR for frontend assets.

### Configuration

- Default config: `src/UmbracoCommunity.Web.UI/appsettings.Development.json`
- Local overrides: Create `appsettings.Local.json` (gitignored) for API keys and connection strings
- Default database: SQLite (`umbraco/Data/Umbraco.sqlite.db`)
- Unattended install credentials: community@umbraco.com / community!

### Building

```bash
# Build entire solution
dotnet build

# Build frontend assets (production)
cd src/UmbracoCommunity.StaticAssets
npm run build

# Build backoffice extensions only
npm run build:backoffice

# Build for cloud deployment (copies files via devops/copy-for-cloud.js)
npm run build:for:cloud

# Build Extensions backoffice client (separate build)
cd src/UmbracoCommunity.Extensions/Client
npm run build
```

### Testing

Frontend tests use Vitest:

```bash
cd src/UmbracoCommunity.StaticAssets

# Run tests
npm run test

# Run tests with UI
npm run test:ui

# Run tests with coverage
npm run test:coverage
```

Test files are colocated with components (`.test.ts` suffix).

## Architecture Patterns

### Pages (Document Types)

Each Umbraco page follows this structure:
1. **Document Type** - created in Umbraco backoffice
2. **Controller** - route hijacking (in `Controllers/`)
3. **View Model** - inherits from `PageViewModelBase` (in `Models/Pages/`)
4. **View Model Builder** - converts IPublishedContent to view model (in `ViewModelBuilders/Pages/`)
5. **View** - Razor template (in `UmbracoCommunity.Web.UI/Views/`)

See `docs/BUILDING_PAGES.md` for detailed instructions.

### Content Blocks

Reusable components using Umbraco's Block List editor:
1. **Element Type** - Umbraco content structure
2. **Settings Type** - optional configuration
3. **View Model** - strongly-typed model (in `Models/Blocks/`)
4. **View Model Builder** - converts content to view model (in `ViewModelBuilders/Blocks/`)
5. **View** - Razor partial (in `Views/Partials/Blocks/`)

See `docs/BUILDING_BLOCKS.md` for detailed instructions.

### Frontend Assets

Located in `src/UmbracoCommunity.StaticAssets/src/`:
- **components/** - Lit web components (including `sessionize/` for event components)
- **entrypoints/** - Vite entry points (files starting with `_*.ts`)
- **css/** - PostCSS stylesheets with custom rhythm mixin system
- **services/** - Frontend services (fetch, logging, project/user services, sessionize service)
- **integrations/** - Third-party integrations (Cookiebot, Intercom, Matomo, Google Maps)
- **models/** - TypeScript data models
- **types/** - TypeScript type definitions
- **util/** - Utility functions
- **assets/** - Static assets (images, SVGs)
- **plugins/** - Vite plugins
- **test/** - Test utilities

Built assets go to:
- Frontend: `dist/` → referenced in views
- Backoffice: `../UmbracoCommunity.Web.UI/wwwroot/App_Plugins/UmbracoCommunityGitHubUsers/`

**PostCSS Rhythm System**: Custom mixin (`postcss-rhythm.mixin.ts`) generates spacing utility classes like `.pt-md`, `.m-xs`, `.mx-lg` based on CSS custom properties with modifiers: `-xxs`, `-xs`, `-sm`, (default), `-md`, `-lg`, `-xl`, `-0`.

### Models Builder

Models are generated in **SourceCodeManual** mode (development) / **Nothing** mode (production):
- Namespace: `UmbracoCommunity.Web.Models.PublishedModels`
- Directory: `src/UmbracoCommunity.Web/Models/PublishedModels/`
- After creating document types in backoffice, manually generate models

## Key Features

### GitHub Sync System

Located in `Features/GitHubSync/`, this system:
- Uses Entity Framework Core with dual database support (SQLite/SQL Server)
- Syncs GitHub issues, PRs, discussions, and NuGet package data via Hangfire jobs
- Runs on scheduled intervals (hourly for recent data, weekly for full sync)

**Background Jobs:**
- `FetchRecentPullRequestsJob` / `FetchAllPullRequestsJob`
- `FetchRecentIssuesJob` / `FetchAllIssuesJob`
- `FetchReleaseDiscussionsJob`
- `FetchRecentNuGetPackageVersionsJob`
- `FetchHqMembersJob`

Job configuration: `Features/GitHubSync/Configuration/JobsComposer.cs`

### Release Overview System

Located in `Features/ReleaseOverview/`, this feature displays Umbraco CMS release information:

**Controllers:**
- `ReleaseController` - Individual release pages (implements `IVirtualPageController`)
- `ReleasesHomeController` - Release landing page
- `AllReleasesController` - List of all releases
- `CompareController` - Compare releases

**Virtual Page Pattern**: `ReleaseController` implements `IVirtualPageController` which allows pages to exist without Umbraco content nodes - routes are handled programmatically via custom route composers.

**Views**: Located in `Views/Partials/ReleaseOverview/`

### Sessionize Integration

Located in `Features/Sessionize/`, this feature integrates with the Sessionize platform for event management:

**Backend Components:**
- **Configuration**: `RegisterSessionize.cs` - Composer that registers options and API client
- **API Client**: `SessionizeApiClient.cs` - Service with caching for fetching sessions, speakers, schedules
- **API Controller**: `SessionizeApiController.cs` - REST endpoints at `/api/sessionize`
- **Models**: `SessionizeAllData`, `SessionizeSchedule`, `SessionizeSession`, `SessionizeSpeaker`

**API Endpoints** (`/api/sessionize`):
- `GET /sessions` - All sessions
- `GET /sessions/{sessionId}` - Specific session
- `GET /speakers` - All speakers
- `GET /speakers/{speakerId}` - Specific speaker
- `GET /schedule` - Grid schedule by date/time/room
- `GET /categories` - Event categories

**Frontend Components** (`src/UmbracoCommunity.StaticAssets/src/components/sessionize/`):
- `sessionize-program.element.ts` - Program grid display
- `sessionize-speakers.element.ts` - Speakers grid with filtering
- `sessionize-session-dialog.element.ts` - Session details modal
- `sessionize-speaker-dialog.element.ts` - Speaker details modal

**Configuration** (in `appsettings.json`):
```json
"Sessionize": {
  "EventId": "",
  "BaseUrl": "https://sessionize.com/api/v2/",
  "CacheDurationInMinutes": 60
}
```

### Backoffice Extensions

Located in `UmbracoCommunity.Extensions/`, provides custom Umbraco backoffice functionality:

**API Controllers** (`Controllers/`):
- HQ Members CRUD operations
- GitHub data export/import
- Contribution statistics
- Release summaries

**Backoffice Dashboards** (`Client/src/dashboards/`):
- Main dashboard with contribution stats
- CMS contribution analytics dashboard
- Data management (import/export GitHub data)
- HQ members management

The Extensions project has its own TypeScript/Vite build in `Client/` with output to `wwwroot/App_Plugins/UmbracoCommunityExtensions/`.

### Release Management

Releases are tracked via GitHub Discussions in the Umbraco CMS repository:
- Created automatically by GitHub Actions when `release/*` labels are added
- Release managers update discussion body with release date and LTS status
- Community site syncs every hour
- Release pages display aggregated issues/PRs/discussions by version

See `docs/RELEASES_MANAGEMENT.md` for details.

### Vite Integration

Custom Vite integration for Umbraco:
- Manifest-based asset loading in development and production
- Helper in `Vite/` directory for generating script/style tags
- PostCSS with custom rhythm mixin for consistent spacing
- Dual build modes: frontend website (`npm run build`) + backoffice extensions (`BUILD_TARGET=backoffice`)

### Security Headers

Uses `Joonasw.AspNetCore.SecurityHeaders` for CSP and security headers:
- Custom CSP builder extensions in `Extensions/CspBuilderExtensions.cs`
- Nonce-based script security via `NonceTagHelper`
- CSP can be disabled per-request via `DisableCspMiddleware`
- HSTS enabled with preload and 1-year max age

## Key Dependencies

**Backend:**
- Umbraco CMS 17.1.0 on .NET 10
- Entity Framework Core 10.0.2 (SQLite + SQL Server providers)
- Cultiv.Hangfire 5.2.0 - Background job processing
- Joonasw.AspNetCore.SecurityHeaders 6.0.0 - Security headers middleware
- Markdig 0.44.0 - Markdown processing
- Schema.NET 13.0.0 - Structured data/schema markup
- Umbraco.Community.BlockPreview 5.1.0 - Block preview in backoffice
- Umbraco.Community.Contentment 6.0.2 - Extended content editors

**Frontend:**
- Lit 3.3.0 - Web components framework
- RxJS 7.8.1 - Reactive programming
- Zod 4.x - TypeScript schema validation
- Vite 7.x - Build tool and dev server
- Vitest - Testing framework
- PostCSS with custom rhythm mixin system

## Code Conventions

This section documents coding standards used in this project. Items marked with **[Umbraco]** are official Umbraco best practices from the [Umbraco documentation](https://docs.umbraco.com). Items marked with **[Project]** are conventions specific to this codebase.

### File Organization [Project]

1. **One class per file**: Each class, record, or interface should have its own file. Never nest public classes inside other files (e.g., DTOs should not be defined at the bottom of controller files).

2. **Namespaces match folders**: Namespace should reflect the folder structure. For example:
   - `Controllers/Api/BlogApiController.cs` → `namespace UmbracoCommunity.Web.Controllers.Api`
   - `Models/Api/BlogPostDto.cs` → `namespace UmbracoCommunity.Web.Models.Api`

### Naming Conventions

| Component | Pattern | Example | Source |
|-----------|---------|---------|--------|
| Document Type Alias | PascalCase | `Blog`, `Article` | [Umbraco] |
| Render Controller | `{DocumentTypeAlias}Controller` | `BlogController` | [Umbraco] - Required for route hijacking |
| View | `{DocumentTypeAlias}.cshtml` | `Blog.cshtml` | [Umbraco] - Required for route hijacking |
| Page View Model | `{DocumentTypeAlias}PageViewModel` | `BlogPageViewModel` | [Project] |
| View Model Builder | `{DocumentTypeAlias}PageViewModelBuilder` | `BlogPageViewModelBuilder` | [Project] |
| Block Partial | `{ElementTypeAlias}.cshtml` | `TextBlock.cshtml` | [Umbraco] |
| API Controller | `{Feature}ApiController` | `BlogApiController` | [Project] |
| TypeScript Component | `{name}.element.ts` | `blog-posts-list.element.ts` | [Project] |
| Web Component Tag | `dc-{kebab-case-name}` | `<dc-blog-posts-list>` | [Project] |

### Controller Organization [Project]

Controllers are organized by type in subfolders (this is a project convention, not an Umbraco requirement):

- **API Controllers** (`Controllers/Api/`):
  - Inherit from `ControllerBase` (standard ASP.NET Core)
  - Have the `[ApiController]` and `[Route("api/...")]` attributes
  - Return JSON responses via `Ok()`, `NotFound()`, etc.
  - Use `IUmbracoContextFactory` for content access
  - Use `[OutputCache]` for server-side caching (see Output Caching section below)
  - Note: `UmbracoApiController` was removed in Umbraco 14+; use standard ASP.NET Core controllers

- **Render Controllers** (`Controllers/Render/`):
  - Inherit from Umbraco's `RenderController`
  - **Controller name must match document type alias** [Umbraco] - required for route hijacking
  - Return views via `CurrentTemplate(viewModel)`
  - Use `[ApplyCommonElements]` and `[ApplyPageMetaData]` attributes [Project]

- **Plain MVC Controllers** (root `Controllers/`):
  - Inherit from `Controller`
  - Handle non-Umbraco routes (e.g., `RobotsController`)

### Route Hijacking [Umbraco]

Route hijacking is an official Umbraco pattern. Key requirements from [Umbraco docs](https://docs.umbraco.com/umbraco-cms/reference/routing/custom-controllers):

1. **Controller name MUST match document type alias** - e.g., `BlogController` for document type `Blog`
2. **Inherit from `RenderController`** - provides access to `CurrentPage` and `UmbracoContext`
3. **Template = Action name** - if no matching action, falls back to `Index()`
4. **For async controllers**: Mark sync `Index()` as `[NonAction]` and `sealed override`

```csharp
public class BlogController : RenderController
{
    private readonly IViewModelBuilder<BlogPageViewModel> _viewModelBuilder;

    public BlogController(
        ILogger<BlogController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IViewModelBuilder<BlogPageViewModel> viewModelBuilder)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
        => _viewModelBuilder = viewModelBuilder;

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    [ApplyCommonElements]
    [ApplyPageMetaData]
    public IActionResult Index(CancellationToken cancellationToken)
    {
        var viewModel = _viewModelBuilder.Build(
            CurrentPage ?? throw new InvalidOperationException(),
            UmbracoContext);
        return CurrentTemplate(viewModel);
    }
}
```

### ViewModelBuilder Pattern [Project]

This project uses a custom `IViewModelBuilder<T>` pattern to convert `IPublishedContent` to view models. This is a **project-specific pattern**, not an official Umbraco recommendation.

The official Umbraco approach suggests view models can:
- Inherit from `PublishedContentWrapped`
- Inherit from ModelsBuilder-generated types (e.g., `class MyViewModel : BlogPage`)
- Be plain POCOs populated in the controller

Our builder pattern provides separation of concerns:

```csharp
internal class BlogPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<BlogPageViewModel>
{
    public BlogPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        var contentModel = currentPage.As<Blog>();
        return new BlogPageViewModel(currentPage)
        {
            // Map properties from contentModel to view model
        };
    }
}
```

Register builders in `Extensions/UmbracoBuilderExtensions.cs`:
```csharp
builder.Services.AddScoped<IViewModelBuilder<BlogPageViewModel>, BlogPageViewModelBuilder>();
```

### Model Organization [Project]

- **API DTOs** (`Models/Api/`): Data transfer objects for API requests/responses
- **Page View Models** (`Models/Pages/`): View models for Umbraco pages, inherit from `PageViewModelBase`
- **Block View Models** (`Models/Blocks/`): View models for block list items
- **Component View Models** (`Models/ViewModels/Components/`): Reusable view model components
- **Published Models** (`Models/PublishedModels/`): Auto-generated by Models Builder (do not edit directly)

### Models Builder [Umbraco]

Per [Umbraco docs](https://docs.umbraco.com/umbraco-cms/reference/templating/modelsbuilder):

- Use **SourceCodeManual** mode for development (generates `.cs` files on demand)
- Use **Nothing** mode in production appsettings
- Models are generated as **partial classes** - extend them in separate files, don't modify generated code
- Keep extensions stateless and local to the model - don't add request-dependent logic

### View Conventions

- **Page views** [Umbraco]: Use `@inherits UmbracoViewPage<TViewModel>` for strongly-typed access
- **Block partials** [Umbraco]: Located in `Views/Partials/Blocks/`, named after element type alias
- **Component partials** [Project]: Located in `Views/Partials/Components/`
- **Layouts** [Project]: Use `Layout.cshtml` for main site, `LayoutReleases.cshtml` for release pages

### Content Access Patterns [Umbraco]

From [Umbraco docs](https://docs.umbraco.com/umbraco-cms/implementation/services):

| Context | Use | Why |
|---------|-----|-----|
| Render Controllers | `IUmbracoContextAccessor` | Context exists on HTTP request thread |
| API Controllers | `IUmbracoContextFactory` | May not have existing context; creates one if needed |
| Action Filters | `IUmbracoContextAccessor` | Running within HTTP request |
| Background Jobs | `IUmbracoContextFactory` | No HTTP request; must create context explicitly |
| Singleton Services | `IUmbracoContextFactory` | Enables use outside request scope |

**Important**: Never inject `UmbracoContext` directly into constructors. Always use the accessor or factory.

```csharp
// In API controller or background service
using var cref = _umbracoContextFactory.EnsureUmbracoContext();
var cache = cref.UmbracoContext.Content;
var node = cache?.GetById(1234);
```

### Dependency Injection [Umbraco]

From [Umbraco docs](https://docs.umbraco.com/umbraco-cms/reference/using-ioc):

- Use `IUmbracoBuilder` extension methods for registering services
- Register services with appropriate lifetimes:
  - **Scoped**: Services using `IUmbracoContextAccessor`, ViewModelBuilders
  - **Singleton**: Stateless utilities, `IUmbracoContextFactory`-based services
  - **Transient**: Lightweight, stateless services

### Composers [Umbraco]

From [Umbraco docs](https://docs.umbraco.com/umbraco-cms/implementation/composing):

Use Composers for Umbraco-specific startup customization:

```csharp
public class MyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<IMyService, MyService>();
    }
}
```

Best practices:
- **Don't use type scanning** if avoidable - it increases boot time
- Use `[ComposeBefore]` and `[ComposeAfter]` attributes for ordering
- Don't put business logic in composers - only registration code
- Use `[Disable]` attribute to disable composers from packages if needed

### Feature Modules [Project]

Self-contained features are organized in `Features/` with their own subfolders:
- `Controllers/` - Feature-specific controllers (follow same conventions)
- `Models/` - Feature-specific models
- `Infrastructure/` - Services, clients, configuration
- `Configuration/` - Composers and DI registration

This keeps related code together while maintaining the same conventions within each feature.

### Feature-Scoped Constants [Project]

Avoid magic strings by using feature-scoped constants. Keep constants close to the feature that owns them:

**GitHub Sync Constants** (`Features/GitHubSync/GitHubConstants.cs`):
```csharp
// Release label patterns
GitHubConstants.ReleaseLabels.Prefix  // "release/"
GitHubConstants.ReleaseLabels.Infix   // "/release/"

// Cache key generation
GitHubConstants.CacheKeys.AvailableReleases("Umbraco-CMS")
GitHubConstants.CacheKeys.NuGetVersions("Umbraco.Cms")
```

**Release Overview Routes** (`Features/ReleaseOverview/ReleaseRoutes.cs`):
```csharp
ReleaseRoutes.ReleasePattern      // "release/{org}/{repo}/{version}"
ReleaseRoutes.ComparePattern      // "compare"
ReleaseRoutes.AllReleasesPattern  // "all-releases"
ReleaseRoutes.Names.Release       // Route names for URL generation
ReleaseRoutes.Controllers.Release // Controller names
```

**Content Type Aliases**: Use Models Builder's `ModelTypeAlias` constants:
```csharp
// Good - compile-time checked
Article.ModelTypeAlias
Blog.ModelTypeAlias

// Bad - magic string
"article"
```

**Cross-cutting Constants**: Use partial `Constants` class for security/infrastructure:
- `Constants.Security` - CSP domains, allowed image types

### Output Caching for APIs [Project]

API endpoints use .NET Output Caching for server-side response caching. This is more powerful than Response Caching and properly handles route parameters.

**Available Policies** (defined in `OutputCachePolicies` class):

| Policy | Default Duration | Use For | Invalidation |
|--------|------------------|---------|--------------|
| `ContentDriven` | 24 hours | Blog posts, Umbraco content-driven APIs | Umbraco notifications (on publish/unpublish) |
| `ExternalApi` | 300 seconds (5 min) | External integrations (Sessionize), third-party APIs | Time-based expiration only |

**Configuration** (in `appsettings.json`):
```json
"OutputCache": {
  "ContentDrivenDurationSeconds": 86400,
  "ExternalApiDurationSeconds": 300
}
```

Development uses shorter durations - see `appsettings.Development.json`.

**Usage:**
```csharp
// For Umbraco content-driven APIs (cache invalidated when content changes)
[HttpGet("posts/{blogKey:guid}")]
[OutputCache(PolicyName = OutputCachePolicies.ContentDriven)]
public IActionResult GetPosts(Guid blogKey, [FromQuery] int page = 1)

// For external integrations (time-based expiration only)
[HttpGet("sessions")]
[OutputCache(PolicyName = OutputCachePolicies.ExternalApi)]
public async Task<IActionResult> GetSessions()
```

**Cache Invalidation via Umbraco Notifications** [Umbraco]:

Content-driven caches are automatically invalidated when relevant content is published or unpublished. This uses Umbraco's `ContentCacheRefresherNotification`:

- `BlogContentCacheInvalidationHandler` listens for content cache refresh events
- When `Article` or `Blog` content types change, it evicts the `OutputCacheTags.BlogContent` tag
- This allows long cache durations while ensuring content updates appear immediately

To add cache invalidation for new content types:
1. Add the content type alias to the handler's `BlogContentTypeAliases` set
2. Or create a new handler for different cache tags

**Important:** Don't use `[ResponseCache]` with `VaryByQueryKeys` or `VaryByHeader` on API controllers - these require Response Caching middleware which isn't configured. Use `[OutputCache]` instead.

### Frontend (TypeScript/Lit) [Project]

- **Web Components**: Use Lit framework, file suffix `.element.ts`
- **Tag Naming**: Prefix with `dc-` (e.g., `<dc-blog-posts-list>`)
- **Services**: Located in `services/`, use `ServiceBase` for HTTP calls
- **Entry Points**: Files starting with `_` in `entrypoints/` folder
- **Tests**: Colocated with components using `.test.ts` suffix

### Action Filters [Project]

Use custom action filters for cross-cutting concerns:
- `[ApplyCommonElements]` - Injects menu and footer into `PageViewModelBase`
- `[ApplyPageMetaData]` - Populates SEO metadata from content
- `[ApplyCommonElementsReleases]` - Variant for release pages

### Middleware [Project]

Custom middleware goes in `Middleware/` folder:
- Register in `WebApplicationExtensions.cs` using `UmbracoPipelineFilter`
- Middleware that needs Umbraco context should run after Umbraco's routing

### References

- [Umbraco Route Hijacking Documentation](https://docs.umbraco.com/umbraco-cms/reference/routing/custom-controllers)
- [Umbraco API Controllers](https://docs.umbraco.com/umbraco-cms/reference/routing/umbraco-api-controllers)
- [Umbraco Services and Helpers](https://docs.umbraco.com/umbraco-cms/implementation/services)
- [Umbraco Dependency Injection](https://docs.umbraco.com/umbraco-cms/reference/using-ioc)
- [Umbraco Composers](https://docs.umbraco.com/umbraco-cms/implementation/composing)
- [Umbraco Models Builder](https://docs.umbraco.com/umbraco-cms/reference/templating/modelsbuilder)
- [Umbraco Cache Notifications](https://docs.umbraco.com/umbraco-cms/reference/notifications/cacherefresher-notifications)

## Important Notes

- **Git branch**: Main branch is `develop` (not main/master)
- **Database migrations**: Run automatically on startup via `DatabaseMigrationHostedService`
- **Security**: Never commit `appsettings.Local.json` - it's gitignored for secrets
- **Frontend dev**: Always run both dotnet and npm dev servers for full HMR experience
- **Models**: Remember to manually generate Models Builder classes after backoffice changes
- **Action Filters**: Use `[ApplyCommonElements]` and `[ApplyPageMetaData]` attributes on controllers for consistent page rendering
- **Output Caching**: API endpoints use `[OutputCache]` with policy names from `OutputCachePolicies` class
- **Upgrade Tool**: Use `tools/upgrade-umbraco/` for package version upgrades

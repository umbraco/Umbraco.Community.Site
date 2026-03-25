# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the Umbraco Community Website - a replacement for [community.umbraco.com](https://community.umbraco.com). It's a **multi-tenant** ASP.NET Core application built on Umbraco CMS with a Vite-powered frontend, featuring automated GitHub integration for release tracking, community data synchronization, and Sessionize event integration.

**Multi-tenancy**: The site runs multiple tenants from a single Umbraco instance, each with its own root content node. All content lookups (e.g., site settings, 404 pages, navigation) must be resolved **relative to the current request's content tree** — never assume a single root or use hardcoded paths. Traverse ancestors or use the current request's root node to find tenant-specific content.

## Solution Structure

The solution consists of 5 projects (uses Central Package Management via `Directory.Packages.props`):

- **UmbracoCommunity.Web.UI** - Main web application (startup project)
- **UmbracoCommunity.Web** - Core business logic, features, controllers, view models
- **UmbracoCommunity.StaticAssets** - Frontend assets built with Vite (TypeScript, Lit web components)
- **UmbracoCommunity.Extensions** - Umbraco backoffice extensions (Razor Class Library with TypeScript client in `Client/` folder)
- **UmbracoCommunity.BlockRestrictions** - Block restrictions functionality

### Key Directories in UmbracoCommunity.Web

- **Features/** - Self-contained feature modules (Sessionize) with their own controllers, models, and infrastructure
- **Controllers/** - MVC controllers organized by type:
  - `Controllers/Api/` - API controllers (inherit from `ControllerBase`, have `[ApiController]` attribute)
  - `Controllers/Render/` - Umbraco render controllers (inherit from `RenderController`)
  - Root folder - Plain MVC controllers (inherit from `Controller`)
- **Models/** - View models, content models, and published content models
  - `Models/Api/` - DTOs for API request/response objects
  - `Models/Pages/` - Page view models
  - `Models/ContentModels/` - Block/element content models (e.g., `CardsBlock`, `TextBlock`, `VideoBlock`)
  - `Models/ViewModels/` - View models for components (`Components/`), blocks (`Blocks/`), and properties (`Properties/`)
  - `Models/ServiceModels/` - Service-layer models (e.g., `SitemapElement`)
  - `Models/PublishedModels/` - Auto-generated (do not edit)
- **ViewModelBuilders/** - Convert IPublishedContent to view models
  - `ViewModelBuilders/Pages/` - Page-specific view model builders
  - `ViewModelBuilders/Components/` - Component view model builders (Menu, Footer)
  - `ViewModelBuilders/Schema/` - SEO schema builders (`ArticleSchemaBuilder`, `BreadcrumbSchemaBuilder`, `OrganizationSchemaBuilder`)
- **Services/** - Application services (`ContentDataService`, `ContentContextService`, `ISeoDataService`/`SeoDataService`)
- **ViewComponents/** - ASP.NET Core View Components for layout concerns (MetaTags, Menu, Footer, Favicon)
- **Routing/** - Custom content finders (e.g., `PageNotFoundContentFinder`)
- **Extensions/** - Extension methods for ASP.NET Core builders, Umbraco helpers, CSP, and HTML helpers
- **Middleware/** - Custom middleware (CSP handling)
- **Utilities/** - Helper classes (`ReleaseDiscussionParser`, `ReleaseLabelHelper`, `SemVerHelper`, `StringUtilities`, `UrlUtilities`)
- **Helpers/** - Domain helpers (ColourHelper, ImageHelper, VideoHelper)
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
3. **Content Model** - strongly-typed model (in `Models/ContentModels/`)
4. **View Model** - view-layer model (in `Models/ViewModels/Blocks/`)
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

### Sessionize Integration

Located in `Features/Sessionize/`, this feature integrates with the Sessionize platform for event management:

**Backend Components:**
- **Configuration**: `RegisterSessionize.cs` - Composer that registers options and API client
- **API Client**: `SessionizeApiClient.cs` - Service with caching for fetching sessions, speakers, schedules
- **API Controller**: `SessionizeApiController.cs` - REST endpoints at `/api/sessionize`
- **Models**: `SessionizeAllData`, `SessionizeSchedule`, `SessionizeSession`, `SessionizeSpeaker`, `SessionizeQuestion`, `SessionizeQuestionAnswer`

**Speaker Pronouns**: Pronouns are extracted from Sessionize's `questionAnswers` data. The top-level `questions` array is used to find the "Pronouns" question ID (resolved once and cached on `SessionizeAllData.PronounsQuestionId`), then each speaker's `questionAnswers` is checked for a matching answer. Pronouns are displayed in the speakers grid, speaker dialog, and session dialog.

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
- Blog article creation

**Backoffice Dashboards** (`Client/src/dashboards/`):
- Sessionize dashboard

The Extensions project has its own TypeScript/Vite build in `Client/` with output to `wwwroot/App_Plugins/UmbracoCommunityExtensions/`.

### Vite Integration

Custom Vite integration for Umbraco:
- Manifest-based asset loading in development and production
- Helper in `Vite/` directory for generating script/style tags
- PostCSS with custom rhythm mixin for consistent spacing
- Dual build modes: frontend website (`npm run build`) + backoffice extensions (`BUILD_TARGET=backoffice`)

### SEO and Schema Markup

The site implements structured data using Schema.NET for better search engine visibility:

**Schema Builders** (`ViewModelBuilders/Schema/`):
- `OrganizationSchemaBuilder` - Builds Organization schema from site settings (multi-tenant configurable, falls back to Umbraco defaults)
- `ArticleSchemaBuilder` - Builds Article schema for blog posts with headline, datePublished, dateModified, author, image, publisher
- `BreadcrumbSchemaBuilder` - Builds BreadcrumbList schema from content hierarchy

**Meta Tags** (`Views/Shared/Components/MetaTags/MetaTags.cshtml`):
- Title format: `Page Title | Site Name` (page-first for better SERP visibility)
- Open Graph tags: og:title, og:description, og:type, og:site_name, og:locale, og:image, og:url
- Twitter Cards: summary_large_image format
- Canonical URLs with pagination support (prev/next links)
- Robots meta tag with configurable directives per page

**Configuration**: Organization data is tenant-configurable via `SocialSettings` document type (OrganisationName, OrganisationUrl, OrganisationLogo). Falls back to Umbraco defaults if not configured.

### Security Headers

Uses `Joonasw.AspNetCore.SecurityHeaders` for CSP and security headers:
- Custom CSP builder extensions in `Extensions/CspBuilderExtensions.cs`
- Nonce-based script security via `NonceTagHelper`
- CSP can be disabled per-request via `DisableCspMiddleware`
- HSTS enabled with preload and 1-year max age

## Key Dependencies

**Backend:**
- Umbraco CMS 17.2.2 on .NET 10
- Entity Framework Core 10.0.3 (SQLite + SQL Server providers)
- Joonasw.AspNetCore.SecurityHeaders 6.0.0 - Security headers middleware
- Schema.NET 13.0.0 - Structured data/schema markup
- Umbraco.Community.BlockPreview 5.3.2 - Block preview in backoffice
- Umbraco.Community.Contentment 6.1.1 - Extended content editors

**Frontend:**
- Lit 3.3.0 - Web components framework
- RxJS 7.8.1 - Reactive programming
- Zod 4.x - TypeScript schema validation
- Vite 7.x - Build tool and dev server
- Vitest - Testing framework
- PostCSS with custom rhythm mixin system

## Code Conventions

See [CODE_CONVENTIONS.md](./CODE_CONVENTIONS.md) for detailed coding standards, naming conventions, and architectural patterns used in this project.

## Accessibility

See [ACCESSIBILITY.md](./ACCESSIBILITY.md) for accessibility standards, implementation details, and WCAG conformance information.

## Important Notes

- **Multi-tenant**: Always resolve content (settings, 404 pages, navigation) relative to the current request's root node — never assume a single root or use hardcoded content paths
- **Git branch**: Main branch is `develop` (not main/master)
- **Database**: Uses Entity Framework Core with SQLite/SQL Server support
- **Security**: Never commit `appsettings.Local.json` - it's gitignored for secrets
- **Frontend dev**: Always run both dotnet and npm dev servers for full HMR experience
- **Models**: Remember to manually generate Models Builder classes after backoffice changes
- **ViewComponents over filters**: Layout concerns (menu, footer, meta tags, favicon) are handled by ViewComponents, not action filter attributes
- **Output Caching**: API endpoints use `[OutputCache]` with policy names from `OutputCachePolicies` class
- **Upgrade Tool**: Use `tools/upgrade-umbraco/` for package version upgrades

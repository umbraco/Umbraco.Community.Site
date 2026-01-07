# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the Umbraco Community Website - a replacement for [community.umbraco.com](https://community.umbraco.com). It's an ASP.NET Core application built on Umbraco CMS with a Vite-powered frontend, featuring automated GitHub integration for release tracking and community data synchronization.

## Solution Structure

The solution consists of 4 projects (uses Central Package Management via `Directory.Packages.props`):

- **UmbracoCommunity.Web.UI** - Main web application (startup project)
- **UmbracoCommunity.Web** - Core business logic, features, controllers, view models
- **UmbracoCommunity.StaticAssets** - Frontend assets built with Vite (TypeScript, Lit web components)
- **UmbracoCommunity.Extensions** - Umbraco backoffice extensions (Razor Class Library with TypeScript client in `Client/` folder)

### Key Directories in UmbracoCommunity.Web

- **Features/** - Feature modules (GitHubSync, ReleaseOverview)
- **Controllers/** - Route hijacking controllers
- **Models/** - View models, blocks, and published content models
- **ViewModelBuilders/** - Convert IPublishedContent to view models
- **Attributes/** - Action filters (`ApplyCommonElements`, `ApplyPageMetaData`)
- **Middleware/** - Custom middleware (CSP handling)
- **Utilities/** - Helper classes (SemVerHelper, StringUtilities)
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

# Build for cloud deployment
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
- **components/** - Lit web components
- **entrypoints/** - Vite entry points (files starting with `_*.ts`)
- **css/** - PostCSS stylesheets with custom rhythm mixin system
- **services/** - Frontend services (fetch, logging, project/user services)
- **integrations/** - Third-party integrations (Cookiebot, Intercom, Matomo, Google Maps)
- **models/** - TypeScript data models
- **types/** - TypeScript type definitions
- **util/** - Utility functions

Built assets go to:
- Frontend: `dist/` → referenced in views
- Backoffice: `../UmbracoCommunity.Web.UI/wwwroot/App_Plugins/UmbracoCommunityGitHubUsers/`

**PostCSS Rhythm System**: Custom mixin (`postcss-rhythm.mixin.ts`) generates spacing utility classes like `.pt-md`, `.m-xs`, `.mx-lg` based on CSS custom properties with modifiers: `-xxs`, `-xs`, `-sm`, (default), `-md`, `-lg`, `-xl`, `-0`.

### Models Builder

Models are generated in **SourceCodeManual** mode:
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
- Custom CSP builder extensions in `CspBuilderExtensions.cs`
- Nonce-based script security via `NonceTagHelper`
- CSP can be disabled per-request via `DisableCspMiddleware`

## Key Dependencies

**Backend:**
- Umbraco CMS 17.x on .NET 10
- Entity Framework Core (SQLite + SQL Server providers)
- Cultiv.Hangfire - Background job processing
- Joonasw.AspNetCore.SecurityHeaders - Security headers middleware
- Markdig - Markdown processing
- Schema.NET - Structured data/schema markup

**Frontend:**
- Lit 3.x - Web components framework
- RxJS - Reactive programming
- Zod - TypeScript schema validation
- Vitest - Testing framework
- PostCSS with custom rhythm mixin system

## Important Notes

- **Git branch**: Main branch is `develop` (not main/master)
- **Database migrations**: Run automatically on startup via `DatabaseMigrationHostedService`
- **Security**: Never commit `appsettings.Local.json` - it's gitignored for secrets
- **Frontend dev**: Always run both dotnet and npm dev servers for full HMR experience
- **Models**: Remember to manually generate Models Builder classes after backoffice changes
- **Action Filters**: Use `[ApplyCommonElements]` and `[ApplyPageMetaData]` attributes on controllers for consistent page rendering

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
- **Controllers/** - Route hijacking controllers
- **Models/** - View models, blocks, and published content models
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
- **svg/** - Reusable SVG icons as Lit templates (`lucide-icons.ts`)
- **plugins/** - Vite plugins
- **test/** - Test utilities

Built assets go to:
- Frontend: `dist/` → referenced in views
- Backoffice: `../UmbracoCommunity.Web.UI/wwwroot/App_Plugins/UmbracoCommunityGitHubUsers/`

**PostCSS Rhythm System**: Custom mixin (`postcss-rhythm.mixin.ts`) generates spacing utility classes like `.pt-md`, `.m-xs`, `.mx-lg` based on CSS custom properties with modifiers: `-xxs`, `-xs`, `-sm`, (default), `-md`, `-lg`, `-xl`, `-0`.

**Dialog System** (`src/components/dialog/`):
- `dialog-base.element.ts` - Base class for modal dialogs
- `dialog.handler.ts` - Opens/closes dialogs, manages body scroll lock with scrollbar width compensation to prevent content jump

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
- `sessionize-program.element.ts` - Program grid display with deep linking support
- `sessionize-speakers.element.ts` - Speakers grid with filtering
- `sessionize-session-dialog.element.ts` - Session details modal with social sharing
- `sessionize-speaker-dialog.element.ts` - Speaker details modal

**Session Sharing & Deep Linking:**
- URLs support `?session={sessionId}` parameter for direct session linking
- When visiting a URL with session parameter, page scrolls to program and opens session dialog
- Session dialog includes share buttons for LinkedIn, Bluesky, Mastodon, and copy link
- Server-side Open Graph tags are generated for shared session URLs via `SeoMetaDataViewModelDecorator`
- Social platforms receive session-specific `og:title`, `og:description`, and `og:url` meta tags

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
- Permissions-Policy configured in `Extensions/WebApplicationExtensions.cs` (includes `clipboard-write=(self)` for share functionality)

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

## Important Notes

- **Git branch**: Main branch is `develop` (not main/master)
- **Database migrations**: Run automatically on startup via `DatabaseMigrationHostedService`
- **Security**: Never commit `appsettings.Local.json` - it's gitignored for secrets
- **Frontend dev**: Always run both dotnet and npm dev servers for full HMR experience
- **Models**: Remember to manually generate Models Builder classes after backoffice changes
- **Action Filters**: Use `[ApplyCommonElements]` and `[ApplyPageMetaData]` attributes on controllers for consistent page rendering
- **Response Caching**: Sessionize API endpoints use `[ResponseCache]` for performance
- **Upgrade Tool**: Use `tools/upgrade-umbraco/` for package version upgrades

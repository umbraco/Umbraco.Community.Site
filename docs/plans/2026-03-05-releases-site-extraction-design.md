# Releases Site Extraction Design

**Date:** 2026-03-05
**Goal:** Move the releases feature from the Umbraco Community Site into a standalone Umbraco CMS site at `~/Dev/releases`, then clean up the community site.

## Approach

Straight extraction — no architectural changes. Same Umbraco CMS platform, same virtual page pattern, same GitHubSync with Hangfire, same views and frontend assets. The new site is a fresh Umbraco 17 install with all releases code transplanted in.

## New Site Naming

- `Releases.Web.UI` — Startup project (Umbraco host)
- `Releases.Web` — Core business logic
- `Releases.StaticAssets` — Vite frontend
- `Releases.Extensions` — Backoffice extensions

## What Moves (removed from community site)

### Features
- `Features/ReleaseOverview/` — controllers, route composers, models, configuration
- `Features/GitHubSync/` — jobs, API clients, entities

### Controllers
- `ReleaseController` (virtual page, `/release/{org}/{repo}/{version}`)
- `ReleasesHomeController` (render controller)
- `AllReleasesController` (virtual page, `/all-releases`)
- `CompareController` (virtual page, `/compare`)

### View Model Builders
- `ReleasePageViewModelBuilder`
- `ReleasesHomePageViewModelBuilder`
- `AllReleasesPageViewModelBuilder`
- `ComparePageViewModelBuilder`
- `MenuReleasesViewModelBuilder`

### Models
- All `Features/ReleaseOverview/Models/` (ReleaseGroupViewModel, ReleasePullRequestViewModel, etc.)
- `ReleasePageViewModel`, `ReleasesHomePageViewModel`, `AllReleasesPageViewModel`, `ComparePageViewModel`
- `MenuReleasesViewModel`
- Extensions models: `ReleaseInfo`, `ReleaseSummary`

### Views
- `LayoutReleases.cshtml`
- `SingleRelease.cshtml`, `HomeReleases.cshtml`, `AllReleases.cshtml`
- All `Views/Partials/ReleaseOverview/` partials
- `MenuReleases.cshtml`, `HeaderReleases.cshtml`, `FooterReleases.cshtml`, `LogoReleases.cshtml`, `LogoSvgReleases.cshtml`

### Frontend Assets
- `_releaseshome.ts` (entrypoint)
- `releaseshome.css`

### Attributes
- `ApplyCommonElementsReleasesAttribute`

### Utilities
- `ReleaseDiscussionParser`
- `ReleaseLabelHelper`
- `SemVerHelper`

### Extensions Project
- Release-related backoffice controllers and dashboards
- Release-related models (`ReleaseInfo`, `ReleaseSummary`)
- Associated TypeScript client code

### Database
- GitHub database file (auto-recreated by Hangfire jobs on first run)

## What Gets Copied (needed by both sites)

These are shared infrastructure pieces that the community site also uses for other features:

- Vite integration (tag helpers, models in `Vite/`)
- `PageViewModelBase`, `ViewModelBuilderBase`, `IViewModelBuilder<T>`, `IPageViewModelDecorator<T>`
- `ApplyCommonElements`, `ApplyPageMetaData`, `ApplyFilterBase`
- CSP middleware & extensions (`CspBuilderExtensions`, `DisableCspMiddleware`)
- `NonceTagHelper`, `SvgTagHelper`
- `_ViewImports.cshtml`, `_ViewStart.cshtml`
- Schema builders (`OrganizationSchemaBuilder`, `BreadcrumbSchemaBuilder`)
- Output cache policies
- `ContentDataService`
- Vite config, PostCSS rhythm system, package.json base dependencies
- `Constants.Security`, `Constants.Culture`
- Menu/Footer base view models and builders

## Community Site Cleanup

After extraction:

1. Delete `Features/ReleaseOverview/` and `Features/GitHubSync/`
2. Delete all release views, partials, layouts
3. Delete `_releaseshome.ts`, `releaseshome.css`
4. Delete `ApplyCommonElementsReleasesAttribute`
5. Delete all release-specific view model builders, models, utilities
6. Delete `MenuReleasesViewModel`, `MenuReleasesViewModelBuilder`
7. Delete release-related Extensions controllers/dashboards/models
8. Clean up `Program.cs` / composition (remove GitHubSync registration, release route composers)
9. Remove dependencies only used by releases (Hangfire)
11. Delete GitHub database file
12. Remove releases content nodes from Umbraco backoffice

## New Site Structure

```
releases/
├── src/
│   ├── Releases.Web.UI/
│   │   ├── Program.cs
│   │   ├── Views/
│   │   │   ├── LayoutReleases.cshtml
│   │   │   ├── SingleRelease.cshtml
│   │   │   ├── HomeReleases.cshtml
│   │   │   ├── AllReleases.cshtml
│   │   │   ├── Partials/
│   │   │   │   ├── ReleaseOverview/
│   │   │   │   └── Components/
│   │   │   ├── _ViewImports.cshtml
│   │   │   └── _ViewStart.cshtml
│   │   └── appsettings*.json
│   ├── Releases.Web/
│   │   ├── Features/
│   │   │   ├── ReleaseOverview/
│   │   │   └── GitHubSync/
│   │   ├── Attributes/
│   │   ├── Extensions/
│   │   ├── Middleware/
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── TagHelpers/
│   │   ├── Utilities/
│   │   ├── Vite/
│   │   └── ViewModelBuilders/
│   ├── Releases.StaticAssets/
│   │   ├── src/
│   │   │   ├── entrypoints/_releaseshome.ts
│   │   │   └── css/releaseshome.css
│   │   ├── vite.config.ts
│   │   └── package.json
│   └── Releases.Extensions/
│       ├── Controllers/
│       ├── Models/
│       └── Client/
├── Directory.Packages.props
└── Releases.sln
```

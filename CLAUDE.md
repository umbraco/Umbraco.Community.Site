# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the Umbraco Community Website - a replacement for [community.umbraco.com](https://community.umbraco.com). It's a **multi-tenant** ASP.NET Core application built on Umbraco CMS with a Vite-powered frontend, featuring Sessionize event integration and aggregated community content feeds (blog posts, calendar). (Release tracking used to live here too but has been extracted to a separate releases site.)

**Multi-tenancy**: The site runs multiple tenants from a single Umbraco instance, each with its own root content node. All content lookups (e.g., site settings, 404 pages, navigation) must be resolved **relative to the current request's content tree** — never assume a single root or use hardcoded paths. Traverse ancestors or use the current request's root node to find tenant-specific content.

## Documentation

The repo carries a small library of conceptual and operational docs alongside the code:

- **[docs/primers/](docs/primers/)** — concept-oriented overviews that give the lay of the land for an area (frontend, backend, multi-tenancy, backoffice, caching, and integrations are written; content modelling and SEO are still planned stubs). Start here if you're new to the codebase.
- **[docs/tutorials/](docs/tutorials/)** — short, focused deep dives on specific problems we've hit and how we solved them. Split into [`foundations/`](docs/tutorials/foundations/) (standalone patterns) and [`refinements/`](docs/tutorials/refinements/) (improvements layered on a foundation).
- **[docs/BUILDING_PAGES.md](docs/BUILDING_PAGES.md)** and **[docs/BUILDING_BLOCKS.md](docs/BUILDING_BLOCKS.md)** — how-tos for adding new pages and content blocks.
- **[docs/LESSONS_LEARNED.md](docs/LESSONS_LEARNED.md)** — workflow gotchas (Umbraco upgrades, schema management, urgent fixes).
- **[CODE_CONVENTIONS.md](CODE_CONVENTIONS.md)** and **[ACCESSIBILITY.md](ACCESSIBILITY.md)** — coding standards and WCAG conformance notes.

Both `docs/primers/` and `docs/tutorials/` carry their own `IDEAS.md` backlog of planned material, with a placeholder file under the relevant folder for each entry. When picking one up to write, expand the existing stub in place rather than creating a new file.

## Solution Structure

The solution consists of 6 projects (uses Central Package Management via `Directory.Packages.props`):

- **UmbracoCommunity.Web.UI** - Main web application (startup project)
- **UmbracoCommunity.Web** - Core business logic, features, controllers, view models
- **UmbracoCommunity.StaticAssets** - Frontend assets built with Vite (TypeScript, Lit web components)
- **UmbracoCommunity.Extensions** - Umbraco backoffice extensions (Razor Class Library with TypeScript client in `Client/` folder)
- **UmbracoCommunity.BlockRestrictions** - Block-level content restrictions (Razor Class Library with EF Core migrations and backoffice client in `Client/` folder)
- **Umbraco.Community.NotFoundTracker** - 404 tracking with ignore rules and a dashboard (Razor Class Library with EF Core migrations and backoffice client in `Client/` folder)

### Key Directories in UmbracoCommunity.Web

- **Features/** - Self-contained feature modules (Sessionize, Feeds, Mvp, Seed) with their own controllers, models, and infrastructure
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
- **Routing/** - Custom routing (e.g., `CommunitySitePageNotFoundResolver` for tenant-aware 404s, `DocumentationContentFinder`)
- **Extensions/** - Extension methods for ASP.NET Core builders, Umbraco helpers, CSP, and HTML helpers
- **Middleware/** - Custom middleware (CSP handling)
- **Utilities/** - Helper classes (`StringUtilities`, `UrlUtilities`)
- **Helpers/** - Domain helpers (ColourHelper, CountryFlagHelper, VideoHelper)
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

# Build for cloud deployment (copies files via devops/copy-for-cloud.js)
npm run build:for:cloud

# Build Extensions backoffice client (separate build)
cd src/UmbracoCommunity.Extensions/Client
npm run build

# Build NotFoundTracker backoffice client (separate build)
cd src/Umbraco.Community.NotFoundTracker/Client
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

Reusable components using Umbraco's Block List / Block Grid editor:
1. **Element Type** - Umbraco content structure (the block's properties)
2. **Settings Type** - optional configuration (often composing `ISettingsColour`, `ISettingsBlockId`)
3. **Content Model** - the Models Builder published model; optionally extended by a hand-written partial in `Models/ContentModels/` for view-only helpers (e.g. `IdHash`, computed properties)
4. **View** - Razor partial in `Views/Partials/Blocks/{Alias}.cshtml` inheriting `BlockGridItem<TContent, TSettings>` and binding the content/settings models directly (there is **no** per-block view model or view-model builder)

See `docs/BUILDING_BLOCKS.md` for detailed instructions.

### Frontend Assets

Located in `src/UmbracoCommunity.StaticAssets/src/`:
- **components/** - Lit web components (including `sessionize/` for event components, `form/` for form enhancements like `<dc-form-steps>`, `image-slider/` for drag-to-scroll image slider)
- **entrypoints/** - Vite entry points (files starting with `_*.ts`)
- **css/** - PostCSS stylesheets with custom rhythm mixin system
- **services/** - Frontend services (fetch, logging, project/user services, sessionize service)
- **integrations/** - Small `script-loader`-based elements for injecting third-party scripts
- **models/** - TypeScript data models
- **types/** - TypeScript type definitions
- **util/** - Utility functions
- **assets/** - Static assets (images, map pins)
- **svg/** - SVG icon modules (arrows, close, grid, lucide icons)
- **plugins/** - Vite plugins
- **test/** - Test utilities

Built assets go to:
- Frontend: `dist/` → copied to `../UmbracoCommunity.Web.UI/wwwroot/assets/` for production, referenced in views

Backoffice extensions are **not** built from this project. Each lives in its own Vite project (`UmbracoCommunity.Extensions/Client/`, `UmbracoCommunity.BlockRestrictions/Client/`, `Umbraco.Community.NotFoundTracker/Client/`) and emits to its own `App_Plugins/<Name>/` folder.

**PostCSS Rhythm System**: Custom mixin (`postcss-rhythm.mixin.ts`) generates spacing utility classes like `.pt-md`, `.m-xs`, `.mx-lg` based on CSS custom properties with modifiers: `-xxs`, `-xs`, `-sm`, (default), `-md`, `-lg`, `-xl`, `-0`.

**Dialog System** (`src/components/dialog/`):
- `dialog-base.element.ts` - Base class for modal dialogs
- `dialog.handler.ts` - Opens/closes dialogs, manages body scroll lock with scrollbar width compensation to prevent content jump

**Text Link Animation**: Inline text links within block content have an animated pink highlight (background-gradient that grows from a 2px underline to full highlight on hover). Defined globally in `typography.css`, scoped to text-only links via `:not(:has(img, svg, picture, video, div))`.

**SVG Style Scoping**: Illustrator-exported SVGs ship an inline `<style>` block with auto-generated class names (`.st0`–`.stN`). Inline `<style>` is document-scoped (not SVG-scoped), so without intervention those class rules leak across every SVG on the page and the last-defined `.st0` rule wins for *all* SVGs. The `<svg-src>` TagHelper (`UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`) handles this automatically: it derives a deterministic scope class from the media path, adds it to the `<svg>` element, and prefixes every selector inside the SVG's `<style>` block with that class. The scoped output is cached in Umbraco's `RuntimeCache` for 60 minutes keyed by media path, so repeat renders skip the file read, sanitisation, parse, and selector-prefix work. External CSS that targets `.stN` (e.g. the hero-mode logo overrides in `header.css`) still works because it sits outside the SVG and the scoping only adds specificity to the *internal* rules.

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

### Digital Signage

A standalone kiosk view of the Sessionize program for big screens at the venue. The full-screen page intentionally bypasses the usual site chrome.

**Document type & template**:
- `digitalSignagePage` doctype (model: `DigitalSignagePage`) — separate from `contentPage`, composed of `ICompositionContentBlocks` + `ICompositionPageConfiguration` only (no banner, no SEO).
- `Views/digitalSignagePage.cshtml` — `Layout = null`, inherits `UmbracoViewPage<DigitalSignagePage>` directly (no view-model/controller/builder — same pattern as `PageNotFound.cshtml`).
- Editors add `ProgramFeatureBlock` entries to the page's `ContentBlocks` to drive what shows.

**`ProgramFeatureBlock`** (`Views/Partials/Blocks/ProgramFeatureBlock.cshtml`) — picks one of three nested config element types via its `programConfiguration` BlockListItem:
- `CurrentSessionConfig` — current + next per room. The most-used; drives the signage layout.
- `HighlightedSessionsConfig` — explicit session ids picked by the editor.
- `SessionsConfig` — day/room/tag filter intersection.

**`ProgramSessionResolver`** (`Features/Sessionize/Infrastructure/`) — scoped service wrapping `SessionizeApiClient`. Three methods (`ResolveCurrentAsync` / `ResolveHighlightedAsync` / `ResolveFilteredAsync`). Sessionize stores timestamps as UTC; the resolver converts to `Europe/Copenhagen` via the public static `ToEventTime(DateTime)` helper before all comparisons and before display formatting, so a session at `2026-06-11T11:45:00Z` renders as `13:45` on the card. `ResolveCurrentAsync` takes an optional `nowOverride` for the `?signage-now=` testing flow.

**Layout** (`StaticAssets/src/css/pages/digital-signage.css`, `entrypoints/_digital-signage.ts`):
- Codegarden palette override at `:root` (overrides `--color-page-surface`, `--color-identity-orange/yellow`, etc. for the signage scope only).
- Single-room (one room in `CurrentSessionConfig.Rooms`): card pins via `position: fixed; inset: 2rem`, hero-scale typography via `:has(> :only-child)` selectors; current pill features a navy + pink corner triangle motif (inline SVG via `data:` URL, polygons inset via `content-box` padding so the gap shows the pill background — which adapts automatically to the cream `is-empty` variant).
- Multi-room (2-4 rooms): rooms container pins via `inset: 2rem` and tiles cards in a grid sized by `:has(.dc-program-rooms > :nth-child(N):last-child)` (2 → side-by-side, 3 → three cols, 4 → 2×2). Multi-room mode hides the logo and clock, and skips the triangle motif (too busy).
- Card header is a flex row: logo (single-room only, from `Site Settings → HeaderLogo`), room name, clock — all DOM siblings inside the card, not separate fixed-positioned bits.

**Testing the clock rollover**: `?signage-now=2026-06-11T09:55` on the page URL simulates that wall-clock time. The signage JS rebuilds the URL with the *current* simulated time on each 60s poll, so as time progresses past a session boundary the SSR'd state updates accordingly. The clock element is rebound after each poll because the polling swap replaces it.

### Block Restrictions

Located in `UmbracoCommunity.BlockRestrictions/`, this package restricts which block types are available per document type, with content tree inheritance. See [`src/UmbracoCommunity.BlockRestrictions/README.md`](src/UmbracoCommunity.BlockRestrictions/README.md) for full documentation.

**Backend:**
- **Composer**: `BlockRestrictionComposer.cs` - Registers EF Core DbContext, Swagger docs, migration hosted service, and scoped services
- **Service**: `BlockRestrictionService.cs` - Core business logic: resolves restrictions by walking the content tree (ancestor inheritance), caches results, syncs rules to JSON files for version control, zip export/import
- **Infrastructure**: EF Core DbContext (`BlockRestrictionDbContext`), data store (`BlockRestrictionStore`), migration hosted service, file service (`BlockRestrictionFileService`)
- **Controllers**: `BlockRestrictionApiController.cs` — Backoffice Management API endpoints (secured with Umbraco backoffice auth via `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]`)
- **Models**: Response/request DTOs in `Models/` — `AllowedBlocksResponse`, `BlockRestrictionFileModel`, `FileImportModels`, etc.

**Frontend (`Client/`):**
- **Property editors**: `BlockGridRestricted` and `BlockListRestricted` — custom property editor UIs that wrap the native Umbraco block editors with restriction filtering
- **Workspace view**: "Blocks" tab on Document Type workspace for configuring restrictions per document type
- **Dashboard**: "Block Restrictions" section under Settings > Advanced for file import/export, zip export/import, and DB sync
- **Clipboard translators**: Copy/paste support between restricted and native block editors
- **API client**: Typed fetch wrapper with Umbraco backoffice auth integration

**Key concepts:**
- Rules are set per document type and inherited by descendant content nodes in the tree
- Fail-open design: no restrictions = all blocks allowed
- Rules are persisted to both database and JSON files (for version control portability)
- Zip export/import enables portable rule bundles for cloud-hosted environments without direct filesystem access
- Custom property editor UIs: `BlockGridRestricted` and `BlockListRestricted`

**API endpoints** (base: `/umbraco/umbracocommunityblockrestrictions/api/v1`):
- `GET allowed-blocks/{nodeKey}` — resolve effective restrictions for a content node
- `GET/PUT/DELETE rules/{docTypeKey}` — CRUD for restriction rules
- `GET element-types` — list all element types
- `GET block-data-types` — list restricted block data types
- `GET file-import/preview` — preview file-to-DB diff
- `POST file-import/apply` — apply file import to DB
- `POST file-import/export-rule/{docTypeKey}` — export single DB rule to disk
- `GET file-import/export-db` — download all DB rules as zip
- `GET file-import/export-files` — download all disk files as zip
- `POST file-import/upload` — upload zip of rule files to disk

### Backoffice Extensions

Located in `UmbracoCommunity.Extensions/`, provides custom Umbraco backoffice functionality:

**API Controllers** (`Controllers/`):
- Blog article creation

**Backoffice Dashboards** (`Client/src/dashboards/`):
- Sessionize dashboard

The Extensions project has its own TypeScript/Vite build in `Client/` with output to `wwwroot/App_Plugins/UmbracoCommunityExtensions/`.

### Stepped Forms

The form block supports an optional multi-step mode via the `EnableSteppedForm` toggle on `SettingsFormBlock`. When enabled, `FormBlock.cshtml` wraps the form in a `<dc-form-steps>` web component (`src/UmbracoCommunity.StaticAssets/src/components/form/form-steps.element.ts`) that:
- Converts Umbraco Forms field groups (`.umbraco-forms-fieldset`) into navigable steps
- Adds Previous/Next buttons and a step indicator
- Validates required fields per step before advancing (supports text, checkbox, radio, and `data-val-required` attributes)
- Uses a MutationObserver to handle async rendering from `<umb-forms-render>`

### Slider Block

The slider block (`SliderBlock`) is a container block that holds nested slide items rendered as a horizontally-scrollable carousel. It supports two slide item types: `SlideItemBlockWithTag` (text tag + title + content) and `SlideItemBlockWithIcon` (icon image + title + content).

**Frontend Components** (`src/UmbracoCommunity.StaticAssets/src/components/sliders/`):
- `dc-slider.element.ts` — Core slider with touch drag-to-scroll (follows finger, snaps to nearest slide on release), hover zone navigation on desktop, and explicit arrow button support. Reused by the Blog Showcase Block — its `closest()` lookup matches both `.dc-slider-block` and `.dc-blog-showcase-block`, and the `has-buttons` class on either ancestor opts into explicit-button mode.
- `dc-slider-controls.element.ts` — Progress bar indicator with zero-padded position labels (`01`...`06`) and a sliding pill showing current position. Hidden when explicit arrow buttons are displayed (`has-buttons` variant)

**Slide item theming** — slides support per-item background colour, background image, or no background (defaults to blue overlay). When the parent slider block has a dark background, slides without their own background render as white cards with blue text. Icon images are inverted to white via CSS filter when the slide background is dark, except on white cards inside dark blocks.

**CSS** (`src/UmbracoCommunity.StaticAssets/src/css/blocks/sliders/`):
- `slider-block.css` — Main slider layout, slide item cards, hover zones, arrow buttons, and breakpoint overrides (mobile-first → `--sm` → `--md` → `--lg` → `--xl`)
- `box-slider.css` / `box-slider-item.css` — Box slider variant styles
- `link-slider.css` / `link-slider-item.css` — Link slider variant styles

**Backoffice preview** — slide items don't have dedicated views under `Views/BlockPreviewApi/BlockGrid/`; they fall back to `Views/Partials/Blocks/SlideItemBlockWithTag.cshtml` / `SlideItemBlockWithIcon.cshtml` via the BlockPreview `ViewLocations` chain in `appsettings.json`. Backoffice-specific slider styles in `wwwroot/css/styles.css` mirror the frontend dark/light theming logic.

### Hero Banner with Image Slider

A hero block combining CTA-style text content with a horizontally-scrollable image gallery.

**Content Properties** (`HeroBannerWithImageSlider`):
- `Headline` (string, max 40 chars) — Main heading
- `Tagline` (string) — Small uppercase label above headline
- `BodyText` (IHtmlEncodedString) — Rich text body content
- `CallToActionLinks` (IEnumerable\<Link\>, max 2) — CTA buttons (Blue + TransparentBlue themes)
- `SliderImages` (IEnumerable\<MediaWithCrops\>) — Images for the slider

**Settings** (`SettingsHeroBannerWithImageSlider`):
- `AutoSlideImages` (bool) — Toggle automatic scrolling of images

**Frontend Component** (`src/UmbracoCommunity.StaticAssets/src/components/image-slider/dc-image-slider.element.ts`):
- `<dc-image-slider>` — Provides mouse drag-to-scroll; touch scrolling handled natively via CSS overflow
- `auto-slide` HTML attribute enables automatic scrolling with infinite loop (cloned children with seamless scroll reset)
- Uses `requestAnimationFrame` + `IntersectionObserver` — pauses when off-screen, during drag/touch, or when the tab is hidden
- Respects `prefers-reduced-motion: reduce`

**CSS** (`src/UmbracoCommunity.StaticAssets/src/css/blocks/hero-image-slider-block.css`):
- Text styling matches the Call to Action block (centred, same responsive font sizes)
- Slider spans full viewport width via `width: 100vw; margin-left: calc(50% - 50vw)` breakout
- Uses design system variables from `root.css`

### Blog Showcase Block

A block that surfaces recent articles from the tenant's Blog page, with optional category/tag filters and a switch between grid and slider layouts.

**Content Properties** (`BlogShowcaseBlock`):
- `Title` / `Subtitle` — via `IContentBlockIntro` mixin
- `BlogCategoryFilter` (IEnumerable\<IPublishedContent\>) — Optional category filter (content picker)
- `BlogTagFilter` (List\<string\>) — Optional tag filter
- `NumberOfPostsToShow` (int) — Defaults to 3, capped at 12 (see `ResolvedNumberOfPostsToShow`)

**Settings** (`SettingsBlogShowcaseBlock`, mixes in `ISettingsBlockId` + `ISettingsColour`):
- `PostDisplay` (dropdown: "Grid" / "Slider") — Layout mode
- `BlockId` — Anchor id (from `ISettingsBlockId`)
- `BackgroundColour` — Block background (from `ISettingsColour`)

**Service** (`Services/BlogService.cs`):
- `GetRecentArticles(currentPage, categoryKeys?, tags?, count)` — Resolves the tenant's `Blog` page via `currentPage.Root().DescendantsOrSelf<Blog>()` and returns its descendants ordered by publish/create date, with optional category/tag filtering.
- `GetBlogPage(currentPage)` — Returns the resolved `Blog` page (used by the partial to render the "Read more on the blog" CTA link).

**View** (`Views/Partials/Blocks/BlogShowcaseBlock.cshtml`):
- Inherits `BlockGridItem<BlogShowcaseBlock, SettingsBlogShowcaseBlock>`
- Card markup is extracted into a `void RenderCard(Article)` local function inside `@functions { ... }` (codebase convention) so grid and slider modes share the same card output
- Slider mode reuses the `<dc-slider>` web component (`.slides-wrapper > .slides > div` structure) and adds `has-buttons` to opt into the explicit-button code path; desktop arrows render inside the intro, mobile arrows inside the slider — same SVG/`data-slider-action` markup as the slider block
- "Read more on the blog" CTA renders as a plain right-aligned text link below the cards (picks up the global `#main-content a:not(.btn)` pink-underline animation)

**CSS** (`src/UmbracoCommunity.StaticAssets/src/css/blocks/blog-showcase-block.css`):
- `.dc-blog-showcase-block--grid` / `--slider` modifiers select between layouts
- Slider variant defines `--blog-showcase-slide-width` per breakpoint (1 / 2 / 3 cards + 20% peek at base / `--sm` / `--md`), with column-gap of `var(--unit)`
- Card hover uses an internal image zoom (`__media img` → `scale(1.06)`) instead of card-level `transform: scale()`, so the slides-wrapper's `overflow: hidden` doesn't clip the rounded corners
- `has-bg` / `bg-dark` rules mirror the slider block's pattern; backoffice preview rules in `wwwroot/css/styles.css` are kept in sync

### Rich Text Editor Style Menu

A custom tiptap toolbar extension is defined in `App_Plugins/RichtextStyles/umbraco-package.json`. It provides a "Richtext styles" dropdown with grouped options for headings, inline formatting, blocks, and lists (including condensed list variants that apply a `no-margin` class to `ol`/`ul` tags).

### Site Search

The nav search icon is wired to a `SearchPage` doc type backed by Umbraco's Examine `ExternalIndex`. A typed `SearchService` (`Services/SearchService.cs`) queries the index with multi-tenant scoping — results are filtered to descendants of the current request's content root, so search on Site A doesn't leak Site B hits. HTML is stripped from excerpts before display. The render controller (`Controllers/Render/SearchPageController.cs`) hands the results off to a `SearchPageViewModel` for the `SearchPage.cshtml` view to render.

### Vite Integration

Custom Vite integration for Umbraco:
- Manifest-based asset loading in development and production
- Helper in `Vite/` directory for generating script/style tags
- PostCSS with custom rhythm mixin for consistent spacing
- Single build target here: the public website (`npm run build`). Backoffice extensions build separately, from each `*/Client/` Vite project (library mode, externalising `@umbraco/*`)

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
- Permissions-Policy configured in `Extensions/WebApplicationExtensions.cs` (includes `clipboard-write=(self)` for share functionality)

## Key Dependencies

**Backend:**
- Umbraco CMS 17.4.2 on .NET 10
- Entity Framework Core 10.0.8 (SQLite + SQL Server providers)
- Joonasw.AspNetCore.SecurityHeaders 6.0.0 - Security headers middleware
- Schema.NET 13.0.0 - Structured data/schema markup
- Umbraco.Community.BlockPreview 5.3.2 - Block preview in backoffice
- Umbraco.Community.Contentment 6.1.4 - Extended content editors

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

## Pending follow-ups

Check intermittently and clear as conditions become true.

- **Documentation repository links**: When this repo is made public, set `Documentation:RepositoryUrl` in `appsettings.json` (e.g. `"https://github.com/<owner>/<repo>/blob/develop"`). Until then, repo-relative links from the rendered docs to non-surfaced files (`CODE_CONVENTIONS.md`, `src/...`) render as inert `<code>` rather than dead anchors. Once the config is set, those links rewrite to live GitHub URLs. Source: `DocumentationService.ClassifyRelativeLink` in `src/UmbracoCommunity.Web/Services/Documentation/DocumentationService.cs`.

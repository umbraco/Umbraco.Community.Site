# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the Umbraco Community Website - a replacement for [community.umbraco.com](https://community.umbraco.com). It's a **multi-tenant** ASP.NET Core application built on Umbraco CMS with a Vite-powered frontend, featuring Sessionize event integration and aggregated community content feeds (blog posts, calendar). (Release tracking used to live here too but has been extracted to a separate releases site.)

**Multi-tenancy**: The site runs multiple tenants from a single Umbraco instance, each with its own root content node. All content lookups (e.g., site settings, 404 pages, navigation) must be resolved **relative to the current request's content tree** — never assume a single root or use hardcoded paths. Traverse ancestors or use the current request's root node to find tenant-specific content.

## Documentation

The repo carries a small library of conceptual and operational docs alongside the code:

- **[docs/primers/](docs/primers/)** — concept-oriented overviews that give the lay of the land for an area (frontend, backend, multi-tenancy, backoffice, caching, integrations, content modelling, and SEO are all written). Start with [`getting-started.md`](docs/primers/getting-started.md) if you're new to the codebase.
- **[docs/tutorials/](docs/tutorials/)** — short, focused deep dives on specific problems we've hit and how we solved them. Split into [`foundations/`](docs/tutorials/foundations/) (standalone patterns) and [`refinements/`](docs/tutorials/refinements/) (improvements layered on a foundation).
- **[docs/BUILDING_PAGES.md](docs/BUILDING_PAGES.md)** and **[docs/BUILDING_BLOCKS.md](docs/BUILDING_BLOCKS.md)** — how-tos for adding new pages and content blocks.
- **[docs/LESSONS_LEARNED.md](docs/LESSONS_LEARNED.md)** — workflow gotchas (Umbraco upgrades, schema management, urgent fixes).
- **[CODE_CONVENTIONS.md](CODE_CONVENTIONS.md)** and **[ACCESSIBILITY.md](ACCESSIBILITY.md)** — coding standards and WCAG conformance notes.

Planned-but-unwritten primers or tutorials live as stub files directly under the relevant folder — a `> **Status:** Planned` callout at the top plus a "what this will cover" sketch (e.g. `docs/tutorials/foundations/postcss-mixin-for-design-tokens.md`). When picking one up to write, expand the existing stub in place rather than creating a new file.

## Solution Structure

The solution consists of 10 projects (uses Central Package Management via `Directory.Packages.props`):

- **UmbracoCommunity.Web.UI** - Main web application (startup project)
- **UmbracoCommunity.Web** - Core business logic, features, controllers, view models
- **UmbracoCommunity.StaticAssets** - Frontend assets built with Vite (TypeScript, Lit web components)
- **UmbracoCommunity.Extensions** - Umbraco backoffice extensions (Razor Class Library with TypeScript client in `Client/` folder)
- **UmbracoCommunity.BlockRestrictions** - Block-level content restrictions (Razor Class Library with EF Core migrations and backoffice client in `Client/` folder)
- **Umbraco.Community.NotFoundTracker** - 404 tracking with ignore rules and a dashboard (Razor Class Library with EF Core migrations and backoffice client in `Client/` folder)
- **UmbracoCommunity.BlogAnnouncements** - Discord blog-post announcement pipeline (detection, delivery, tracking store) and its dashboard (Razor Class Library with EF Core migrations and backoffice client in `Client/` folder)
- **UmbracoCommunity.MeetBooking** - Umbraco Forms workflow type that provisions a Google Meet for approved community meeting requests via the community Apps Script in `tools/meet-booking-apps-script/` (Razor Class Library, no client; see its README and `docs/plans/2026-09-03-community-meet-booking-design.md`)
- **UmbracoCommunity.ActiveCampaign** - Umbraco Forms workflow type (*Subscribe to ActiveCampaign*) that adds form submitters to an ActiveCampaign list via the v3 REST API; subscribes immediately (no double opt-in), so forms need a consent checkbox (Razor Class Library, no client; see its README)
- **UmbracoCommunity.SearchTracker** - Logs site-search terms per tenant as daily counts and shows them in a *Search Terms* Content dashboard (Razor Class Library with EF Core migrations and backoffice client in `Client/` folder; see its README)

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
- **TagHelpers/** - Custom tag helpers (SvgTagHelper)
- **Vite/** - Vite integration helpers and tag helpers, including `NonceTagHelper`

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

**Visual regression** (local only): `tests/visual/` is a Playwright harness that screenshots and dumps computed styles for a fixed list of Community and Codegarden pages at 375/768/1280/1440px. Run `npm run baseline` before a CSS change and `npm run compare` after it (needs the site running). See [`tests/visual/README.md`](tests/visual/README.md).

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
- **components/** - Lit web components (including `sessionize/` for event components, `form/` for form enhancements like `<dc-form-steps>`, `image-slider/` for drag-to-scroll image slider, `video-hero/` for the background-video hero)
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

**Design tokens** (`src/css/root.css`): type scale (`--font-size-display-*`, `--line-height-*`, `--font-weight-*`), responsive heading and block-title tokens (`--font-size-h1`…`h4`, `--block-title-*`, `--block-subtitle-*`, redefined per breakpoint in `root.css`), radii (`--radius-card`, `--radius-pill`) and `--measure`. Prefer a token over a literal, and change a size per breakpoint by redefining the token rather than adding a `@media` override in a block. Tokens are runtime custom properties (`preserve: true` in `vite.config.ts`), so they can be overridden in a scope. Dark-background blocks set `--block-heading-color` on their `.bg-dark` rule instead of re-colouring `.block-title`/`.block-subtitle` themselves. Files that use `@media (--sm)` etc. must `@import` `base/media-queries.css` first.

**Tenant themes**: `<body>` carries `data-site` set to the request root's doc type alias (`home` or `eventsHome`, via `ContentContextService.SiteAlias`). Codegarden-only styling goes in `css/themes/codegarden.css` as token overrides under `[data-site="eventsHome"]`, which keeps Community untouched.

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
- **Never hardcode a content type alias as a string literal** (e.g. `"accountPage"`, `c.ContentType.Alias.Equals("searchPage")`). Always reference the generated model's `ModelTypeAlias` constant (e.g. `AccountPage.ModelTypeAlias`) so a doc type alias rename is a compile error, not a silent runtime failure. This is the existing convention throughout the codebase (`Settings.ModelTypeAlias`, `ImageBlock.ModelTypeAlias`, etc.) — apply it everywhere content type aliases are compared, including in Razor views.

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
- Server-side Open Graph tags are generated for shared session URLs via `SeoDataService.ApplySessionOpenGraphOverridesAsync`
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
- External member groups (`ExternalMemberGroupsApiController`, Members section access): `GET`/`PUT members/{memberKey}/external-groups` — read and replace the member groups of a lightweight external member (logic in `UmbracoCommunity.Web/Features/Members/Groups/ExternalMemberGroupService.cs`)
- YouTube video picker (`YouTubeVideoApiController`): `GET youtube/preview`, `POST youtube/posters`, `GET youtube/posters/{mediaKey}` (see "YouTube Videos" below)

**Backoffice Dashboards** (`Client/src/dashboards/`):
- Sessionize dashboard

**Property Editors** (`Client/src/property-editors/`):
- Event schedule (`Umbraco.Plain.Json` schema)
- YouTube video picker (`UmbracoCommunity.YouTubeVideo` schema, see "YouTube Videos" below)

The API client in `Client/src/api/` is generated: run `npm run generate-client` with the site running after changing a controller, rather than editing it by hand.

**Workspace Views** (`Client/src/workspace-views/`):
- "Groups" tab on the member workspace, shown only for external-only members via the `UmbracoCommunity.Condition.IsExternalMember` condition (`Client/src/conditions/`, observes the member workspace context's `kind`). Umbraco 18.3 shows an external member's groups read-only and its Management API refuses to update external members, so this is the only way to assign/remove groups (e.g. "Early access") from the backoffice.
- Entry point (`Client/src/entry-point.ts`) lowers the Umbraco Forms member "Forms" tab (weight 750) to 400 so the member workspace opens on "Info" (500), then Forms, then our "Groups" tab (250). It re-registers the Forms manifest (`Forms.WorkspaceView.Member.Forms`) with a new weight, so revisit it if Forms renames that alias.

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

### YouTube Videos

All YouTube input goes through the **YouTube video picker** data type (schema `UmbracoCommunity.YouTubeVideo`, UI `UmbracoCommunity.PropertyEditorUi.YouTubeVideo`). The **Video** block (`youTubeVideoBlock` + `settingsYouTubeVideoBlock`) uses it, and new video-based blocks should too.

**Editor flow**: paste a link → the picker calls `GET youtube/preview`, which returns the oEmbed title/author and up to four stills (YouTube's thumbnail plus its three auto-generated frames, `maxres*` falling back to `sd*`/`hq*`) → picking a still calls `POST youtube/posters`, which downloads it into the Media library at `Video thumbnails/<site root name>/` (reusing an earlier import by name). Editors can also choose any image from the Media library. Stills are only shown in the backoffice (it has no CSP); the front end serves the imported image, so visitors never load anything from YouTube until they press play.

**Server** (`UmbracoCommunity.Web/Features/YouTube/`):
- `YouTubeVideoPropertyEditor` (schema, reports the poster as a media reference for "Used by"), `YouTubeVideoValue` (stored JSON: `url`, `videoId`, `title`, `posterKey`), `YouTubeVideoValueConverter` → `YouTubeVideo` (`EmbedUrl` on youtube-nocookie with any `?t=` start time, `WatchUrl`, `Poster`)
- `YouTubeVideoValueConnector` makes Umbraco Deploy bring the poster media along
- `YouTubePosterService` only fetches `i.ytimg.com` images for an 11-character video id and a fixed list of variants, so it can't be pointed at other URLs
- Link parsing lives in `VideoHelper.GetYouTubeVideoId` (`watch?v=`, `youtu.be`, `/shorts/`, `/live/`, `/embed/`); the picker relies on the preview endpoint rather than parsing links itself

**Picker element** (`Extensions/Client/src/property-editors/youtube-video/`): inside block editors the document workspace context isn't reachable (block modals render outside it), so the page key for the site folder comes from the `/workspace/document/edit/{key}` route instead.

**Legacy Video block**: the old `videoBlock` ("Video (legacy)", link picker + thumbnail) is kept in the "Legacy" block group so existing videos keep rendering and stay editable. It can't simply be removed from the block editors or restricted — Umbraco then marks existing instances invalid (see the Block Restrictions notes). Instead `LegacyVideoBlockGuard` refuses any save that *adds* a legacy video block (compared by block key against the saved version).

**Rendering**: `Views/Partials/Blocks/YouTubeVideoBlock.cshtml` renders `<dc-video-picker-youtube>` (the legacy block uses it too), which loads nothing from YouTube until the poster is played. Browsers (Chrome included) often ignore `autoplay=1` in an embed, leaving a second click on YouTube's own play button, so on click the component calls `playYouTube()` (`youtube-player.ts`, also used by the Video showcase), which loads YouTube's IFrame Player API once per page (`youtube-api.ts`), creates the player on youtube-nocookie and calls `playVideo()` on ready. Only if YouTube reports `onAutoplayBlocked` (sound refused) does it mute and play, and then it shows a "Turn sound on" button over the player (`.dc-youtube-unmute`, `css/elements/youtube-unmute.css`), which unmutes through the API and removes itself; it also goes if the visitor unmutes with YouTube's own control. If the API can't load it falls back to a plain `autoplay=1` iframe. The poster is cropped to the block's Format (16:9 or 4:3).

### Video Hero

A full-width banner block (`heroBannerWithVideo`) that sits behind the site header and plays a muted, looping background video. It's allowed in the ROOT banner block grid and in the `home` / `eventsHome` Block Restrictions rules.

**Content Properties** (`HeroBannerWithVideo`):
- `Tagline`, `Headline` (max 40 chars, rendered as the page `h1`), `BodyText`, `CallToActionLinks` (max 2, White + Transparent button themes)
- `Video` (Video media type, required) and `MobileVideo` (optional; phones without one get the poster only)
- `PosterImage` (required; crops `desktop` 1920×1080, `desktopSmall` 1280×720, `mobile` 828×1472, preloaded per breakpoint)

**Settings** (`SettingsHeroBannerWithVideo`, Contentment Data List dropdowns):
- `TextAlignment`: `Centre` (default) / `Left`
- `HeroHeight`: `100%` / `90%` (default) / `70%` of the screen
- The view compares these exact strings, so renaming an item value in the backoffice needs a matching change in `HeroBannerWithVideo.cshtml`

**Header hero mode**: `ContentContextService.HasFullWidthHero` adds `hero-full-width` to `<body>` when the page's first banner block is a Video hero. In hero mode `.nav-header` is `position: fixed` (so the hero starts at the top of the page) and the logo, nav and focus ring are white until `body.scroll`. Hero-mode rules live in `header.css` / `header-mobile.css`. Once scrolled the header must look exactly like other pages, so don't add hero-specific scrolled padding or widths.

**Frontend component** (`components/video-hero/dc-video-hero.element.ts`):
- `<dc-video-hero>` reads `data-video` / `data-mobile-video` and picks a source with `(max-width: 767px)`. The `<video>` is `preload="none"` with no `src` until JS sets one, so it never competes with the poster
- Fades the video in on `playing`, and pauses it when off-screen or when the tab is hidden
- `prefers-reduced-motion` or Save-Data: no autoplay; the toggle starts as "Play"
- The pause/play button is required for WCAG 2.2.2. It's hidden when there's no source or the video errors (the poster stays)

**CSS** (`css/blocks/video-hero-block.css`):
- The overlay is never lighter than 55% black, which keeps white text at ≥ 4.5:1 even over a white frame. Don't lighten it to make videos "pop"
- The headline font size is capped at `11vw` so long words fit at 320px
- The content keeps bottom padding clear of the toggle

**Video size limit**: `Constants.Media.MaxHeroVideoBytes` (10 MB) is shared by the preview and `Notifications/HeroVideoSizeValidationHandler`.
- The handler cancels both `ContentSavingNotification` and `ContentPublishingNotification`. Umbraco 18's **Save and publish** goes through `UpdateAndPublishDocumentController`, which a saving-only check doesn't stop
- Persisted block values store a media picker value as a serialised JSON *string*, while editor payloads send an array; the handler parses both

**Backoffice preview** (`Views/BlockPreviewApi/BlockGrid/HeroBannerWithVideo.cshtml`, styles in `wwwroot/css/styles.css`): shows the poster under the same overlay, the text and CTAs, plus pills with each video's name and size. A video over the limit gets a pink `is-warning` pill. New preview views need a rebuild before BlockPreview picks them up.

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

### Video Showcase Block

A Codegarden-only block (in the `eventsHome` Block Restrictions rule) for surfacing past talks from YouTube, with a link out to more on the channel.

**Content** (`VideoShowcaseBlock`): `Title` / `Subtitle` (`IContentBlockIntro`), `Videos` (Block List of `VideoShowcaseItem`, 1–12), `MoreVideosLink` (optional). Each `VideoShowcaseItem` has `Video` (YouTube video picker), `Speaker`, `Edition`, `Description` and an optional `AccentColour` (the shared Background colour picker). The partial in `Models/ContentModels/VideoShowcaseBlock.cs` adds `PlayableVideos` (skips items without a video, caps at 12), `IsGrid(settings)` and the item `Byline`.

**Settings** (`SettingsVideoShowcaseBlock`, with `ISettingsBlockId` + `ISettingsColour`): `Layout`, `list` (default) or `grid`.

**Layouts** (`Views/Partials/Blocks/VideoShowcaseBlock.cshtml`, `css/blocks/video-showcase-block.css`):
- List: a playlist beside one player panel from `--md`, with Previous/Next buttons; below `--md` it's an accordion with each panel under its trigger. Items use `display: contents` on desktop, and panels span `grid-row: 1 / -1` over `repeat(var(--video-count), auto) 1fr` rows, so a tall panel doesn't stretch the list.
- Grid: a bento grid with the first card featured (2×2 from `--md`) and a full-width "More on YouTube" tile. Playing a card makes it the full-width `is-spotlight` card and plays it there.
- Accents cycle through `accent-0`…`accent-3` (blue, pink, orange, yellow); an item's `AccentColour` overrides it through a nonce'd `<style>` rule on the item's id.

**Behaviour** (`components/video-showcase/dc-video-showcase.element.ts`): enhances the server-rendered markup through `data-showcase-*` hooks (`item`, `trigger`, `panel`, `player`, `play`, `step`). Posters are links to YouTube; the element plays them in place through `playYouTube()` (`components/video-pickers/youtube-player.ts`, shared with `<dc-video-picker-youtube>`), so nothing loads from YouTube until a poster is played. Only one video on the page plays at a time, across showcases.

**Backoffice preview**: the block renders a summary card (layout, title, video thumbnails); items use `Views/BlockPreviewApi/BlockList/VideoShowcaseItem.cshtml`. Styles in `wwwroot/css/styles.css`.

### Timeline Block

A roadmap-style block for surfacing phased goals/milestones. Editors create one or more `TimelineEntryBlock` children (each representing a phase label such as "Now", "Next", "Future"), each containing a list of `TimelineGoalBlock` items with a title, optional rich text, and optional tags.

**Content Properties** (`TimelineBlock`):
- `Title` / `Subtitle` — via `IContentBlockIntro` mixin
- `TimelineEntries` (BlockGridModel) — ordered list of `TimelineEntryBlock` children

**Entry** (`TimelineEntryBlock`):
- `EntryTitle` — phase label (rendered as a centred pill on the vertical road line)
- `TimelineGoalItems` (BlockListModel) — ordered list of `TimelineGoalBlock` children

**Goal** (`TimelineGoalBlock`):
- `GoalTitle` (string) — card heading
- `GoalDescription` (IHtmlEncodedString) — optional rich text body
- `GoalTags` (List\<string\>) — optional tag pills; also drive the filter dropdown

**Frontend** (`src/UmbracoCommunity.StaticAssets/src/components/timeline/dc-timeline.element.ts`):
- `<dc-timeline>` — lightweight custom element; wires the `[data-timeline-filter]` select to show/hide `.dc-timeline-goal` cards by `data-tags` attribute

**Layout** (`src/UmbracoCommunity.StaticAssets/src/css/blocks/timeline-block.css`):
- Mobile: single column, no road line
- `--sm`+: vertical pink center line (`::before` on `.dc-timeline`), goals in a 2-column CSS grid (`grid-column: 1` / `2`), pink connector lines (`::after` / `::before` on goal cards) bridging each card to the road. Left connectors attach at `top: 1.5rem`, right connectors at `top: 3.5rem` so they don't join the road at the same point.
- First goal in each entry always starts left (the `goalIndex` counter resets to 0 inside the `@for` loop, not outside it)

**Backoffice preview** (`Views/BlockPreviewApi/BlockGrid/TimelineEntryBlock.cshtml`):
- Renders a single entry (not the full block); always 2-column grid in the preview column, no road line or filter

### Rich Text Editor Style Menu

A custom tiptap toolbar extension is defined in `App_Plugins/RichtextStyles/umbraco-package.json`. It provides a "Richtext styles" dropdown with grouped options for headings, inline formatting, blocks, and lists (including condensed list variants that apply a `no-margin` class to `ol`/`ul` tags).

### Member Authentication (GitHub OAuth)

Community members sign in exclusively via GitHub OAuth — no username/password login. The flow is implemented on top of Umbraco's built-in external login infrastructure.

**Files:**
- `Features/Members/RegisterGitHubAuth.cs` — `IComposer` that registers the GitHub OAuth handler via `AddMemberExternalLogins` / `AddOAuth<GitHubAuthenticationOptions, GitHubAuthenticationHandler>`. Must use `AddOAuth` (not `AddRemoteScheme`) so `OAuthPostConfigureOptions` runs and initialises `StateDataFormat` — using `AddRemoteScheme` directly causes a `NullReferenceException` in `OAuthHandler.BuildChallengeUrl`.
- `Features/Members/GitHubExternalLoginProviderOptions.cs` — configures Umbraco auto-linking with `ExternalOnly = true`: new members are created as Umbraco's [lightweight external members](https://docs.umbraco.com/umbraco-cms/reference/security/lightweight-external-members) (a row in `umbracoExternalMember`, no content node or member-type properties — profile data lives in `MemberProfileStore`, keyed by the member `Key`), auto-approved, added to the `Community Members` group, auto-linked on each sign-in. The `communityMember` alias is still passed to the constructor (required) but unused for external members. For external members Umbraco overwrites `UserName` with the *email* just before `OnExternalLogin`, so that callback must keep setting `UserName` back to the GitHub handle (everything keys off `UserName` = handle). The `OnCreatingTicket` handler in `RegisterGitHubAuth` fetches the member's primary verified email from the GitHub API (the OAuth `email` scope only exposes the public profile email; the `/user/emails` endpoint is needed for private addresses).
- `Features/Members/Migration/ExternalMemberMigrationService.cs` — conversion of existing content-based `communityMember`s to external members, keeping each member's `Key` (so `MemberProfileStore` rows, avatars and the GitHub login link stay attached) and groups. **Runs automatically once per start-up** via `ExternalMemberMigrationStartupJob` (an `IRecurringBackgroundJob`, ~30s after boot, only on the `SchedulingPublisher`/`Single` server role, cross-instance guarded by `ExternalMemberMigrationLease` — a short-lived claim in `umbracoKeyValue` taken under Umbraco's `KeyValues` distributed lock). Switch it off with `ExternalMemberMigration:RunOnStartup = false` (default `true` in `appsettings.json`); once every environment has run it, the job, service and endpoints can be deleted. Manual endpoints remain: `GET /umbraco/umbracocommunityextensions/api/v1/members/external-migration/plan` (dry run) and `POST .../members/external-migration/run?confirm=true[&limit=N]` on `ExternalMemberMigrationApiController` (Extensions project, Members section access, needs a backoffice bearer token). Skips members without a GitHub login, with backoffice comments or property values, or whose key already exists as an external member. Logins are re-saved *after* the swap transaction commits, because Umbraco's `DeleteExternalLoginsOnMemberDeletedHandler` runs post-commit and would wipe them. Until it's run, existing members keep signing in as content members (their login link resolves to the content member first); ExternalOnly only affects newly created members.
- `Features/Members/MvpEarlyAccessLoginHandler.cs` — `MemberLoginSuccessNotification` handler (registered in `RegisterGitHubAuth`) that adds an external member to the `Early access` group on sign-in when their handle (`UserName`, case-insensitive) is listed in the *latest* year of `Features/Mvp/Data/mvp.json`. Add-only: it never removes the group, since editors can also grant it by hand from the Groups tab.
- **Member API gotcha**: `IMemberManager.IsMemberAuthorizedAsync` `int.Parse`s the identity `Id`, which is a GUID for external members, so it throws for them. Check groups with `IMemberManager.GetRolesAsync(member)` instead (as `ProfileAccessService` does). To *change* an external member's groups in the backoffice, use the member's "Member groups" tab (see Backoffice Extensions); Umbraco's own Info view shows them read-only. `GetCurrentMemberAsync`, `GetRolesAsync`, `FindByLoginAsync`, `IsLoggedIn` and `IMemberSignInManager.SignOutAsync` handle both kinds.
- `Controllers/LoginController.cs` — single `POST /logout` action; calls `IMemberSignInManager.SignOutAsync()` which clears all four Umbraco auth cookies (member, external login, 2FA, 2FA remember-me). Plain `HttpContext.SignOutAsync()` is not sufficient.
- `Controllers/Render/AccountPageController.cs` — renders the member's own account page; redirects unauthenticated visitors to `/`. Pulls the member via `IMemberManager.GetCurrentMemberAsync()` and constructs the avatar URL as `https://github.com/{username}.png`.
- `Views/AccountPage.cshtml` + `css/pages/account-page.css` + `entrypoints/_accountpage.ts` — account page view and styles (contact-card layout: blue band, circular avatar, name, GitHub handle, email, sign-out).

**OAuth flow:**
1. Sign-in button in the header POSTs to `UmbExternalLoginController.ExternalLogin` (Umbraco's built-in surface controller) with `provider=UmbracoMembers.GitHub`.
2. Umbraco redirects to GitHub, which calls back to `/signin-github`.
3. `UmbExternalLoginController.ExternalLoginCallback` handles the callback, auto-links or creates the member, and signs them in.

**Header integration:**
- `MenuViewModelBuilder` checks `HttpContext.User.Identity.IsAuthenticated` via `IHttpContextAccessor` and — when signed in — populates `MemberDisplayName` and `MemberAvatarUrl` from the current user's claims.
- `Menu.cshtml` renders a GitHub sign-in button (signed-out) or a circular avatar with a hover dropdown showing the display name and a sign-out form (signed-in). The `enableMemberSignIn` toggle on the tenant's Settings node controls visibility of both states.

**Configuration:**
```json
// appsettings.Local.json (gitignored — never commit)
"GitHub": {
  "ClientId": "<your-app-client-id>",
  "ClientSecret": "<your-app-client-secret>"
}
```
The GitHub OAuth App's callback URL must be set to `https://<host>/signin-github`. If `ClientId` or `ClientSecret` is missing the composer early-returns and GitHub auth is simply not registered (safe for environments without credentials).

### Site Search

The nav search icon is wired to a `SearchPage` doc type backed by Umbraco's Examine `ExternalIndex`. A typed `SearchService` (`Services/SearchService.cs`) queries the index with multi-tenant scoping — results are filtered to descendants of the current request's content root, so search on Site A doesn't leak Site B hits. HTML is stripped from excerpts before display. The render controller (`Controllers/Render/SearchPageController.cs`) hands the results off to a `SearchPageViewModel` for the `SearchPage.cshtml` view to render.

- **Matching:** `SearchQueryBuilder` turns the query into words (letters/digits only, filler words dropped, accents folded) and builds a Lucene `NativeQuery` matching each word exactly, by its root (`speaking` → `speak*`) and, on title-like fields, with small typos. Titles are weighted 3×. The raw query text never reaches Lucene; queries with no usable words fall back to `ManagedQuery`.
- **Searchable pages:** `IPublishedContent.IsSearchable()` (`Extensions/PublishedContentExtensions.cs`) is the one definition, shared by search and suggestions.
- **Suggestions:** with 0 keyword results the page shows up to 4 AI-matched pages; with 1–3, a "Related results" section of up to 3 more. Both come from `ContentSuggestionService` (Umbraco.AI.Search, also behind the 404 page's suggestions via `PageNotFoundSuggestionService`), rendered with `Views/Partials/Components/PageSuggestions.cshtml` and `dc-page-suggestions` styles. Paid lookups are cached per tenant/query and limited by `SuggestionBudget` (no bots, 20/hour per visitor, 60/minute site-wide). Suggestions need a default *Embedding* profile and a built AI index; the Google and Anthropic Umbraco.AI providers are chat-only, so use OpenAI, Microsoft Foundry or Amazon.

See [`docs/tutorials/foundations/site-search-with-examine-externalindex.md`](docs/tutorials/foundations/site-search-with-examine-externalindex.md) for the full walkthrough.

**Search term logging** (`UmbracoCommunity.SearchTracker`): on page 1 of a search, `SearchPageController` calls `ISearchTermRecorder.Record(root.Key, query, total, HttpContext)`. The recorder drops bots, repeat searches and floods, normalises the term (lower-cased, emails and long numbers masked) and queues it; a background writer adds it to one row per site/day/term (`SearchTrackerTermDaily`). No IP, user agent or member id is stored, and a retention sweep deletes rows older than `SearchTracker:RetentionDays` (default 365). The *Search Terms* dashboard in the Content section scopes sites by the user's start nodes. See [`src/UmbracoCommunity.SearchTracker/README.md`](src/UmbracoCommunity.SearchTracker/README.md).

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

### Web Bot Auth Key Directory

`GET /.well-known/http-message-signatures-directory` publishes the Ed25519 public key (JWKS) that Umbraco's crawler signs its requests with, so Cloudflare can verify it as a known bot. The response itself is signed (RFC 9421: `tag="http-message-signatures-directory"`, covered `"@authority";req` and `"content-digest"`, `alg="ed25519"`, `keyid` = JWK thumbprint, `created`/`expires`), freshly on each request with a 5-minute lifetime and `Cache-Control: max-age=60`.

- `Features/WebBotAuth/KeyDirectoryService.cs` (signing, BouncyCastle, as .NET has no Ed25519), `Controllers/WebBotAuthDirectoryController.cs`, route in `UmbracoEndpointBuilderContextExtensions`.
- Config: `WebBotAuth:SigningPrivateKey` = the PKCS8 PEM private key from the Sphere repo's `generate-crawler-signing-key.ts` (same key the crawler uses as `CRAWLER_SIGNING_PRIVATE_KEY`). A secret: `appsettings.Local.json` locally, an environment variable / Cloud secret in deployed environments, never committed. With no key the endpoint returns 404; an invalid key is logged (never the key) and also gives 404.
- Optional: `WebBotAuth:SignatureLifetimeSeconds` (300), `WebBotAuth:CacheMaxAgeSeconds` (60).
- The response also carries `Content-Digest: sha-256=:…:` (RFC 9530) over the body; Cloudflare's `http-signature-directory` validator (crates.io) requires it and checks the signature covers it. `@authority;req` is accepted on responses. Re-run that tool against the live URL after changing anything here.

### Security Headers

Uses `Joonasw.AspNetCore.SecurityHeaders` for CSP and security headers:
- Custom CSP builder extensions in `Extensions/CspBuilderExtensions.cs`
- Nonce-based script security via `NonceTagHelper`
- CSP can be disabled per-request via `DisableCspMiddleware`
- HSTS enabled with preload and 1-year max age
- Permissions-Policy configured in `Extensions/WebApplicationExtensions.cs` (includes `clipboard-write=(self)` for share functionality)

## Key Dependencies

Versions are deliberately **not** listed here — they go stale faster than this file
gets updated. Read them from the manifests instead:

- **Backend** — [`Directory.Packages.props`](./Directory.Packages.props) (Central Package Management, so every version for every project lives in that one file)
- **Frontend** — the `package.json` of the relevant Vite project (`UmbracoCommunity.StaticAssets`, or a `*/Client/` backoffice project)

**Backend:**
- Umbraco CMS on .NET
- Entity Framework Core (SQLite + SQL Server providers)
- Joonasw.AspNetCore.SecurityHeaders - Security headers middleware
- Schema.NET - Structured data/schema markup
- Umbraco.Community.BlockPreview - Block preview in backoffice
- Umbraco.Community.Contentment - Extended content editors

**Frontend:**
- Lit - Web components framework
- RxJS - Reactive programming
- Zod - TypeScript schema validation
- Vite - Build tool and dev server
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

Check intermittently and clear as conditions become true. Nothing outstanding right now — both former entries here (documentation repository links; the article contributors mechanism) turned out to already be resolved on inspection: `DocumentationService.DefaultRepositoryUrl` already defaults to a live, public GitHub URL (`https://github.com/umbraco/Umbraco.Community.Site/blob/develop`), so `ClassifyRelativeLink`'s inert-`<code>` fallback is currently unreachable; and `generate-doc-contributors.js` already attributes per-file from `git log --follow --no-merges`, not "who merged".

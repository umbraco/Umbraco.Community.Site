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

### Title Beside Content

The *Settings - intro layout* composition (`settingsIntroLayout`, toggle `introBesideContent` "Title beside content") lets editors put a block's title in a left column (⅓) beside its content (⅔). It's on the Text, Call to action, Accordion and Form block settings only. Blocks that fill the width (boxes, events, cards) deliberately don't get it.

- `BlockIntroExtensions.IntroLayoutClass` adds `intro-beside` when the toggle is on and the block has a title or subtitle; `Views/Partials/Components/BlockIntro.cshtml` renders the shared `.block-intro`. Text blocks have no title field, so the toggle splits off a *leading heading* in the rich text (`SplitLeadingHeading`); without one the block renders as normal.
- `css/blocks/block-intro.css`: `.block-intro` / `.block-body` are `display: contents` by default, so the stacked layout is unchanged. The grid only applies from `--md`, and only to blocks directly in `.dc-block-list` (not paired rows).
- The title's cap height lines up with the first line of the content: both are trimmed with `text-box: trim-start cap alphabetic`, and the first FAQ item and the form lose their top padding. Firefox ignores `text-box`, so it's a few px off there.
- The toggle has no effect on Image blocks yet.
- *Content block tagline* (`contentBlockTagline`, `IContentBlockTagline`) adds an optional small uppercase label above the title (`.block-tagline` in `typography.css`). It's a separate composition because Banner and Slider blocks already have their own `tagline` property. Only the Accordion block uses it so far; `BlockIntro.cshtml` renders it for any block that composes it.

### Side-by-side Block Rows

`ParseBlockGrid` groups consecutive content blocks into one `BlockGridRow` until their column spans add up to 12, and multi-block rows render with `Views/Partials/BlockRows/{row.Alias}.cshtml` (`ImageTextRow`, `TextRow`, `ImageRow`, `FormTextRow`, `AccordionImageRow`).

- Text, Image, Image with overlay, Form and Accordion blocks can be 4, 6, 8 or 12 columns wide in the ROOT and General content block grids (#287), so editors can pair them as 6 + 6, 4 + 8 or 8 + 4.
- Each row child gets `style="--row-span: {ColumnSpan}"`. From `--md` the row CSS (`image-and-text-block.css`, `faqs-and-image-block.css`) uses it as the flex-grow, so the widths follow the spans. Below `--md` the blocks stack as before.
- A block that isn't paired, for example a lone 8-column block followed by a 12, still renders full width.

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
- Uses `requestAnimationFrame` + `IntersectionObserver` — pauses when off-screen, during drag/touch, while hovered or while focus is inside it, or when the tab is hidden
- Respects `prefers-reduced-motion: reduce`
- Optional pause/play toggle: a `<button data-image-slider-toggle aria-controls="{slider id}" hidden>` anywhere on the page. The slider unhides it only while auto-sliding and swaps its `aria-label` / `is-paused` class. Loop clones are `aria-hidden` and `inert`, so links in them aren't focusable
- Optional arrows: `<button data-image-slider-action="prev|next" aria-controls="{slider id}" hidden>`. The slider unhides them only when it isn't auto-sliding (setting off, or reduced motion), scrolls one image per click and disables each arrow at its end
- The loop clones the images on both sides of the originals and keeps the scroll position within the original set, so the area left of the first image (e.g. a carousel's start padding) shows the previous images rather than an empty gap
- When the images fit inside the slider's padding box (checked on load and resize), it adds `is-static`: no auto-slide, drag, toggle or arrows, and the clones are hidden

**CSS** (`src/UmbracoCommunity.StaticAssets/src/css/blocks/hero-image-slider-block.css`):
- Text styling matches the Call to Action block (centred, same responsive font sizes)
- The shared `dc-image-slider` rules (full-viewport breakout via `width: 100vw; margin-left: calc(50% - 50vw)`, image heights per breakpoint) live in `css/blocks/image-slider.css`
- Uses design system variables from `root.css`

### Image Carousel Block

A standalone image slider for content blocks (`imageCarouselBlock`): the slider from the hero above, without its text. It's allowed in the ROOT content block grid and in the `eventsHome` Block Restrictions rule (Codegarden only).

**Content** (`ImageCarouselBlock`, composes `IContentBlockIntro` for the usual `Title` / `Subtitle`, rendered above the slider): `Images` (required Block List, *Image carousel items* data type, minimum 5 so a looping carousel doesn't look sparse) of `ImageCarouselItem`s:
- `Image` (required), `AltText` (falls back to the media item's name), `Title` (max 40), `Text` (max 255) and `Link` (max 1), all optional
- The content-model partial (`Models/ContentModels/ImageCarouselBlock.cs`) adds `IdHash` and `Items` (items that have an image)

**Settings** (`SettingsImageCarouselBlock`, composes `ISettingsBlockId` + `ISettingsColour`):
- `AutoSlideImages` uses the *On Off Toggle (default on)* data type, so new blocks auto-slide until an editor switches it off
- `ImageSpacing` (*Radio button list - small, medium or large*): `small` (or nothing picked) keeps the slider's normal gap, `medium` is about a tenth of an image's width and `large` about a fifth (`dc-image-carousel--spacing-*` in the view and CSS, which compare these exact values)

**View** (`Views/Partials/Blocks/ImageCarouselBlock.cshtml`):
- Reuses `<dc-image-slider>`; an item with a link renders as an `<a>` (image + caption), otherwise as a `<figure>` with a `<figcaption>`
- Below the slider, right-aligned to the content width: the pause/play toggle when auto-slide is on (WCAG 2.2.2), otherwise previous/next arrows (the shared `.slider-arrow-btn` style from the Slider block)
- A background colour turns the block into a full-width band; dark colours switch the captions and controls to white

**CSS** (`css/blocks/image-carousel-block.css`):
- The block keeps the normal content width. The slider still spans the viewport, but `padding-inline: calc(50vw - 50%)` puts the first image (and, at the end, the last one) on the content edge, so images scroll off the page from there
- The background band reaches the viewport edges with `box-shadow: 0 0 0 100vmax` + `clip-path: inset(0 -100vmax)`, without moving the block out of the content width
- An item's `Title` sits in a blue (`--color-blue`, the footer colour) band across the bottom of the image (`.dc-image-carousel__media`, two lines at most); its `Text` is a small muted caption underneath, kept to the image's width (`width: 0; min-width: 100%`) so it wraps under its image

**Backoffice preview** (`Views/BlockPreviewApi/BlockGrid/ImageCarouselBlock.cshtml`, styles in `wwwroot/css/styles.css`): image thumbnails with their titles, plus pills for the image count, auto-slide state, image spacing and number of linked images, with the thumbnails spaced to match. Inside the block editor each item in the Images list previews with `Views/BlockPreviewApi/BlockList/ImageCarouselItem.cshtml` (thumbnail, title or image name, text, link).

### Persona Picker Block

A generic block (`personaPickerBlock`) where visitors pick who they are from a dropdown and get content written for them: a pitch, reasons, testimonials and an email to send. Built for Codegarden's *Convince your boss* page, so it's only in the `eventsHome` Block Restrictions rule for now.

**Content** (`PersonaPickerBlock`, composes `IContentBlockIntro`): `Personas` (required Block List, *Personas*), plus optional `SelectLabel`, `ReasonsHeading`, `TestimonialsHeading`, `EmailHeading` (each falls back to the Convince your boss wording via the `Resolved…` helpers in `Models/ContentModels/PersonaPickerBlock.cs`) and `Footnote`. Settings (`SettingsPersonaPickerBlock`): `ISettingsBlockId` + `ISettingsColour`.

**Persona** (`Persona`): `PersonaName`, `RoleGroup` (optional, *Dropdown - persona groups*: a fixed list, *My role* / *My Codegarden experience*, so editors can't invent new groups; `RoleGroups()` collects personas into one `<optgroup>` per group in first-seen order (ungrouped personas get no heading), whose label is the item's value, so renaming an item in the data type leaves existing personas on the old text until they're re-picked), `Pitch`, `Reasons` (generic *Title and text items (max 4)* Block List of `TitleAndTextItem`, sized for the 2×2 grid), `Testimonials` (*Testimonial picker (max 4)*, also sized for the 2-column grid), `EmailSubject`, `EmailBody` (plain text, one full email per persona so editors can change each one).

**Testimonials live in the Umbraco 19 Library**: `Testimonial` is an element type with *Allow in Library* on (`Quote`, `PersonName`, `JobTitle`, `Company`), and personas pick them with the Element Picker data type *Testimonial picker (max 4)* (`IEnumerable<IPublishedElement>`, filtered with `OfType<Testimonial>()`). One quote can serve several personas. Keep Codegarden's in the Library's *Codegarden → Testimonials* folder; the Library is shared by all sites, so a user group's *element start node* can limit editors to their site's folder (the picker respects it, as *Ignore user start nodes* is off). Getting the quoted person's permission is the events team's process; the code shows every picked testimonial.

**View** (`Views/Partials/Blocks/PersonaPickerBlock.cshtml`): server-renders every persona as a `<section data-persona="{slug}">` (slugs from `PersonaName`, de-duplicated). The dropdown (`data-persona-field`) is only rendered with more than one persona and starts `hidden`. With more than one persona, every persona except `DefaultPersona` (the first in dropdown order) is rendered `hidden` so the block doesn't flash every persona before the script runs; a `<noscript>` style shows them all again without JavaScript. Empty reasons/testimonials/email sections are left out. The email has *Copy email* (hidden until JS) and an *Open in your email app* `mailto:` link (CRLF line breaks, encoded server-side).

**Behaviour** (`components/persona-picker/dc-persona-picker.element.ts`): unhides the dropdown, shows one persona at a time and keeps the choice in `?persona=` with `history.replaceState`, so a link opens on that persona (unknown values fall back to the first). Copy uses `util/clipboard.ts` and announces the result in a `role="status"` region.

**CSS** (`css/blocks/persona-picker-block.css`): white cards on the page surface; reasons and quotes are one column below `--sm`, two from `--sm`. It resets the global italic `figcaption` style for the quote attribution.

**Backoffice preview**: the block shows the title, the dropdown label and a card per persona with reason/testimonial counts (`Views/BlockPreviewApi/BlockGrid/PersonaPickerBlock.cshtml`); `Persona` and `TitleAndTextItem` list items have their own previews in `Views/BlockPreviewApi/BlockList/`. Styles in `wwwroot/css/styles.css`.

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

**Rendering**: `Views/Partials/Blocks/YouTubeVideoBlock.cshtml` renders `<dc-video-picker-youtube>` (the legacy block uses it too), which loads nothing from YouTube until the poster is played. Browsers (Chrome included) often ignore `autoplay=1` in an embed, leaving a second click on YouTube's own play button, so on click the component calls `playYouTube()` (`youtube-player.ts`, also used by the Video showcase), which loads YouTube's IFrame Player API once per page (`youtube-api.ts`), creates the player on youtube-nocookie and calls `playVideo()` on ready. Only if YouTube reports `onAutoplayBlocked` (sound refused) and the video still isn't playing 750 ms later (YouTube can report a block for its own autoplay attempt even when playback then goes ahead with sound) does it mute and play, and then it shows a "Turn sound on" button over the player (`.dc-youtube-unmute`, `css/elements/youtube-unmute.css`), which unmutes through the API and removes itself; it also goes if the visitor unmutes with YouTube's own control. If the API can't load it falls back to a plain `autoplay=1` iframe. The poster is cropped to the block's Format (16:9 or 4:3).

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

### Tile Grid Block

A grid of tiles of different sizes (stats, text, images, a button), laid out by the editor. Allowed on `eventsHome` (Codegarden) only.

**Content** (`TileGridBlock`): `Title` / `Subtitle` (`IContentBlockIntro`), `Tiles` (Block Grid "Tile grid tiles", 12 columns, only `TileGridItem`). Editors set each tile's width (3 / 4 / 6 / 8 / 12 columns) and height (1 or 2 rows) with Block Grid's own resizing.

**Tile** (`TileGridItem` + `SettingsTileGridTile`): `TileContent` is a Block List ("Tile content items") of `EyebrowBlock` (small uppercase label), `StatBlock` (`Number`, `Label`, `LabelBesideNumber`), and the existing `TextBlock`, `ImageBlock`, `IconBlock` and `CallToActionLinkBlock`. `TileContent` is optional. Settings: `BackgroundColour` (`ISettingsColour`), `BackgroundImage` with `BackgroundImagePosition` (`fill` / `right` / `bottom-right`) and `BackgroundImageAltText`, and `ContentAlignment` (`top` / `bottom` / `spread`). The view compares those exact values, so renaming an item value in the backoffice needs a matching change in `TileGridItem.cshtml`.

**Layout** (`css/blocks/tile-grid-block.css`): tiles get their spans as `--tile-columns` / `--tile-rows` style properties. Below `--sm` every tile is full width; at `--sm` tiles up to 6 columns are half width and wider ones full width (`--tile-columns-sm`, set in the view); from `--md` the real spans and row spans apply. `spread` uses `space-between`, so put a label first and a stat or footnote last (an Eyebrow block works as a footnote). A `fill` image under text gets a 55% black overlay and white text so the text stays readable. A tile with a background image and no content is an image-only tile: the image always fills it, with no overlay, and its `BackgroundImageAltText` setting becomes the image's alt text (empty = decorative). *Right half* and *Bottom right* images sit beside the text only from `--md` on tiles at least 6 columns wide (`dc-tile--wide-md`); otherwise they stack under it. A tile whose colour (white when it has none) is too close to the block's to tell apart (`ColourHelper.ContrastRatio` under 1.1) gets `dc-tile--outlined`, a faint blue border (white on dark colours); the block passes its colour to the tiles as `ViewData["TileGridBackground"]`. The nested tile preview in the block's edit dialog doesn't get that colour, so it shows no outline.

**Content partials**: `StatBlock`, `EyebrowBlock` and `IconBlock` render from `Views/Partials/Blocks/`, which the tile and the BlockPreview `ViewLocations` fallback both use. The button is `Views/Partials/Components/CallToActionLink.cshtml` (model: link, text, colour), shared with the rich text editor's CTA. An Image block honours *Image is circular*.

**Backoffice preview**: the block and tiles render through their front-end partials. Styles in `wwwroot/css/styles.css`.

### Video Showcase Block

A Codegarden-only block (in the `eventsHome` Block Restrictions rule) for surfacing past talks from YouTube, with a link out to more on the channel.

**Content** (`VideoShowcaseBlock`): `Title` / `Subtitle` (`IContentBlockIntro`), `Videos` (Block List of `VideoShowcaseItem`, 1–6), `MoreVideosLink` (optional). Each `VideoShowcaseItem` has `Video` (YouTube video picker), `Speaker`, `Edition`, `Description`. The partial in `Models/ContentModels/VideoShowcaseBlock.cs` adds `PlayableVideos` (skips items without a video, caps at `MaxVideos` = 6), `IsGrid(settings)`, `HasPlaylistOnLeft(settings)` and the item `Byline`.

**Settings** (`SettingsVideoShowcaseBlock`, with `ISettingsBlockId` + `ISettingsColour`): `Layout`, `list` (default) or `grid`; `PlaylistPosition` (generic "Radio button list - left or right" data type), `right` (default) or `left`, list layout only.

**Layouts** (`Views/Partials/Blocks/VideoShowcaseBlock.cshtml`, `css/blocks/video-showcase-block.css`): the block has no surface of its own; only the editor's Background colour (`has-bg`) gives it one, and videos never get their own colours (one pink accent for the play button and current number). "More on YouTube" is a pill row inside the component.
- List: player panel plus playlist from `--md` (`--playlist-left` modifier swaps the columns), with Previous/Next buttons; below `--md` it's an accordion where the open item is filled solid and its header closes it again (side by side, the first video reopens if none is open). Items use `display: contents` on desktop, and the open panel spans `grid-row: 1 / -1` over `repeat(var(--video-count), auto) 1fr` rows and stretches, so its footer (watch link + arrows) lines up with the "More" link in the last `1fr` row.
- Grid: a plain card grid (1 / 2 / 3 columns); each card's title link (stretched over the card) opens a native `<dialog>` rendered once per block, which plays the video with a "02 / 06" count and Previous / Up next buttons (thumbnail + title) to step through the videos as a playlist. The next video starts when one ends (`playYouTube`'s `onEnded`). The dialog is full screen below `--sm`, and its player is capped at `52dvh` tall so the playlist row stays on screen. Card data for the dialog lives in `data-*` attributes on each `data-showcase-item`.

**Behaviour** (`components/video-showcase/dc-video-showcase.element.ts`): enhances the server-rendered markup through `data-showcase-*` hooks (`item`, `trigger`, `panel`, `player`, `play`, `step`; grid: `open`, `dialog`, `dialog-*`, `close`). Closing the dialog stops the video, unlocks scrolling (`body.dialog-open`) and puts focus back on the card last played. Posters are links to YouTube; the element plays them in place through `playYouTube()` (`components/video-pickers/youtube-player.ts`, shared with `<dc-video-picker-youtube>`), so nothing loads from YouTube until a poster is played. Only one video on the page plays at a time, across showcases.

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
- `Features/Members/EarlyAccessLoginHandler.cs` — `MemberLoginSuccessNotification` handler (registered in `RegisterGitHubAuth`) that adds an external member to the `Early access` group on sign-in when their handle (`UserName`, case-insensitive) is listed in the *latest* year of `Features/Mvp/Data/mvp.json`, or when the external content platform marks them as a teammate (`isTeammate` on the profile lookup; the lookup waits at most 4 seconds and a failure just means "not this time"). Add-only: it never removes the group, since editors can also grant it by hand from the Groups tab.
- **Member API gotcha**: `IMemberManager.IsMemberAuthorizedAsync` `int.Parse`s the identity `Id`, which is a GUID for external members, so it throws for them. Check groups with `IMemberManager.GetRolesAsync(member)` instead (as `ProfileAccessService` does). To *change* an external member's groups in the backoffice, use the member's "Member groups" tab (see Backoffice Extensions); Umbraco's own Info view shows them read-only. `GetCurrentMemberAsync`, `GetRolesAsync`, `FindByLoginAsync`, `IsLoggedIn` and `IMemberSignInManager.SignOutAsync` handle both kinds.
- **Forum badges** (`Features/Profiles/ForumBadges/`): the account page's *Forum badges* panel (`<dc-profile-badges-editor>`, `components/profile/`, `css/elements/profile-badges-editor.css`) lists the member's Umbraco forum (Discourse) badges with a checkbox each; the chosen ones (at most `ForumBadgeService.MaxShown` = 8) show as a row of chips under the link pills in the profile hero (`_ProfileHero.cshtml`, `ProfileHeroViewModel.Badges`), on the public profile and live in the account page header; the row is left out when none are chosen, and the compact bar doesn't show it. Each chip links to the badge's page on the forum (`ForumBadge.Url` → `ForumProfileUrl.BadgeUrl`: `https://forum.umbraco.com/badges/{id}/{slug}`, built from the numeric id and a plain slug from `BadgeSlug`, the forum's own slug or one made from the name, never from a forum-supplied URL), opened in a new tab with `rel="noopener noreferrer"`; the url travels in `ProfileBadge.Url` / the API's `url`, so `dc-account-profile-hero` renders the same anchor live. The forum account comes only from the member's own forum link under *Feeds & links* (`ForumProfileUrl.FindUsername`: the first non-removed `https://forum.umbraco.com/u/{username}` link, shown ones before hidden ones, trailing path/query/fragment ignored, username checked by `IsValidUsername`: letters, digits, `._-`, max 60). There is deliberately no fallback to the GitHub handle: forum usernames can differ, and a same-named stranger's badges must never show. `ForumBadgeService.GetMemberBadgesAsync` is the one place this is resolved, for the API, `MemberProfileDataProvider` and so `AccountProfileIdentityService`. `ForumBadgeClient` (typed `HttpClient`, fixed `https://forum.umbraco.com/`, 4s timeout, no redirects) reads the anonymous public `user-badges/{username}.json` and caches per username in `IMemoryCache` (1 hour, failures 5 minutes); any failure is logged and returned as *unavailable*, never thrown. Badge pictures (`image_url`, often a forum-relative `/uploads/...` path) only reach the browser through the feed-submission image proxy; a badge with only a FontAwesome icon name gets the lucide *award* icon (`Views/Partials/Icons/Award.cshtml` / `iconAward`). Choices are stored in `MemberProfileEntity.ShownForumBadgeIds` (JSON column, migration `AddShownForumBadges`); `MemberProfileDataProvider` fills `CommunityProfile.Badges` with the chosen badges the member still holds, and only asks the forum when some are chosen. API: `GET`/`PUT /api/profile/badges` (`ProfileBadgesApiController`, current member, antiforgery; ids the member doesn't hold are dropped, more than 8 is a 400, an unreachable forum a 503). Statuses: `ok`, `noForumLink` (the panel explains how to add the link, linking to `#feeds`), `notFound` (no forum user by that name) and `unavailable`; with no badges it says *no badges yet*. The panel reloads on `dc-member-feeds-changed` and re-announces the shown badges, so adding, changing or removing the forum link updates the header row; chosen ids the new account doesn't hold are ignored, not deleted.
- `Controllers/LoginController.cs` — single `POST /logout` action; calls `IMemberSignInManager.SignOutAsync()` which clears all four Umbraco auth cookies (member, external login, 2FA, 2FA remember-me). Plain `HttpContext.SignOutAsync()` is not sufficient.
- `Middleware/OnboardingRedirectMiddleware.cs` + `Features/Profiles/ProfileAutoOnboardingService.cs` — automatic onboarding. The first page request from a signed-in member whose profile isn't `Completed` (no row, `NotStarted` or `InProgress`) sets the profile up and 302s them straight to the tenant's `AccountPage` (`GetSingletonPage<AccountPage>()`, no `returnUrl`; no redirect when the request is already the account page or the tenant has none). Setup: get-or-create the `MemberProfileStore` row, fetch the GitHub profile once (`GitHubProfileClient`, cached via `SetGitHubDefaultsAsync`), write the GitHub name (`member.Name`, else the handle), company and bio as the member's *own* `DisplayNameOverride` / `Headline` / `Bio` (`FillMissingProfileTextAsync`, only fields still null, so earlier edits survive), then `CompleteOnboardingAsync`. `IsProfileVisible` is left alone, so profiles stay hidden until the member shows them. A failed GitHub call still completes with the name; any failure is logged and the request continues. Completed members cost one profile lookup per page request. Non-page requests (`/umbraco`, `/media`, `/api/`, `/logout`, static files) are skipped.
- `Controllers/Render/OnboardingPageController.cs` + `Views/OnboardingPage.cshtml` (+ `css/pages/onboarding-page.css`) — the onboarding page only shows anonymous visitors a *Sign in with GitHub* prompt (useful as a shared "get your profile" link); signed-in members are redirected to their account page. The old onboarding wizard and its `POST /api/profile/onboarding/start|complete` endpoints are gone; the doc type's `devRelContactForm` property is no longer rendered. If automatic setup failed, the account page's *Profile* panel says so and asks the member to reload (the middleware retries on every page request); it never links to the onboarding page.
- `Controllers/Render/AccountPageController.cs` — renders the member's own account page; redirects unauthenticated visitors to `/`. Pulls the member via `IMemberManager.GetCurrentMemberAsync()` and constructs the avatar URL as `https://github.com/{username}.png`.
- `Views/AccountPage.cshtml` + `css/pages/account-page.css` + `entrypoints/_accountpage.ts` — account page view and styles: a header that previews the public profile hero (the shared `Views/Partials/Profiles/_ProfileHero.cshtml` + `css/elements/profile-hero.css`, also used by `CommunityProfilePage.cshtml`; identity and shown forum badges from `AccountProfileIdentityService.GetHeaderAsync`: the same `IProfileDataProvider` data plus MVP years), with the handle, email, a *View public profile* button and a note while the profile is hidden. `<dc-account-profile-hero>` keeps it live: the services announce changes on `window` (`dc-member-feeds-changed` from `MemberFeedsService`, `dc-profile-details-changed` / `dc-profile-visibility-changed` from `ProfileOnboardingService`, `dc-profile-badges-changed` from `ProfileBadgesService`, like the avatar's `dc-profile-avatar-changed`), so a link added anywhere (e.g. *Add to my links* on a link we found, which `dc-feed-manager` also picks up) shows as a pill straight away. `dc-feed-manager` has no plain Remove: a link only comes off through *This isn't me* in the row's edit mode, whose confirmation calls `DELETE /api/member-feeds/{id}`, which removes the link *and* rejects its address as a source (`ProfileLinkMatchingService.RejectSourceAsync`), so activity found through it stops showing and it is never suggested again (suggestions skip removed links too); adding it back by hand takes back the member's own rejection (`UndoMemberRejectionAsync`), not one made in the match review dashboard. Then a side menu (`<dc-account-nav>`, `components/account/`) with one panel open at a time — `#profile`, `#timeline`, `#add-activity`, `#feeds`, `#badges`, `#mvp-badge` (only those the member is offered; *Badges* once onboarding is done) — and sign-out at the bottom of the menu. The URL hash picks the panel; without JavaScript all panels show and the menu links are in-page anchors. Below `--md` the menu is a horizontally scrolling row. The *Timeline* panel's `dc-profile-display-editor` draws the member's items as the public profile timeline does, with the shared `css/elements/profile-timeline.css` and `components/profile/profile-timeline-entry.ts` (kind icons in `svg/profile-kind-icons.ts`, kept in sync with `_KindIcon.cshtml`); `GET /api/profile/display` carries each item's timeline details (icon, url, meta, card, month, order) built with `ProfileTimeline` / `ProfileTimelinePreviewService`. Reorderable lists (feeds, pins) share `components/sortable/sortable-list.ts` + `css/elements/sortable.css` (grab handle, up/down buttons, drop line).
- **Name, headline and bio are the member's own text, with no fallbacks** (`MemberProfileEntity.DisplayNameOverride` / `Headline` / `Bio`). GitHub only supplies the starting values, copied in once at onboarding; after that nothing ever falls back to the cached `GitHubBio` / `GitHubCompany` (kept on the row, never shown) and the Profile panel's `dc-profile-details-editor` has no placeholders, "use GitHub" or "don't show" controls for these fields (the photo's *Use GitHub photo* is a separate thing). The name is required: `PUT /api/profile/display-name` returns 400 for empty/whitespace, `ProfileOnboardingService.updateDisplayName` refuses it without a request, and the editor marks the field `aria-invalid` with an inline message and disables Save. `PUT headline` / `PUT bio` accept empty text, stored as null, and a null headline or bio simply isn't shown (profile hero, account header live via `dc-profile-details-changed`, `ProfileSeo.Description`). `ProfileDetailsResponse` is just `{ displayName, headline, bio }`. Migration `CopyGitHubTextIntoOwnProfileText` copied the cached GitHub company/bio/name into still-null own fields once, so profiles kept what the old fallback showed; for completed profiles whose GitHub values were never fetched, the account page runs `GitHubProfileDefaultsService.BackfillFromGitHubOnceAsync` (one fetch per member, fills only still-null headline/bio, failures logged).

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

The nav search icon is wired to a `SearchPage` doc type backed by Umbraco's Examine published-content index (`Umb_PublishedContent`, resolved through `IActiveIndexManager`). A typed `SearchService` (`Services/SearchService.cs`) queries the index with multi-tenant scoping — results are filtered to descendants of the current request's content root, so search on Site A doesn't leak Site B hits. The index also holds member-protected pages, so only hits whose stored `Sys_Protection` is public are returned. Fields are named `Field_{alias}_{kind}` (see `PublishedContentIndexFields`), and block editors index their text already extracted. HTML is stripped from excerpts before display. The render controller (`Controllers/Render/SearchPageController.cs`) hands the results off to a `SearchPageViewModel` for the `SearchPage.cshtml` view to render.

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

### Open Badges (Umbraco MVP badge)

Current MVPs (by `ProfileMvp.Resolve`, the same platform-years-else-`mvp.json` rule as the profile's MVP pill) get a "Download Umbraco MVP {year} badge" section on the account page: an Open Badges 2.0 PNG with the assertion JSON baked into an `openbadges` iTXt chunk (`PngBadgeBaker`, by hand), for uploading to Sessionize. Code in `Features/OpenBadges/`; public JSON-LD at `/badges/issuer.json`, `/badges/mvp/{year}.json`, `/badges/assertions/{id}.json` and artwork at `/badges/mvp/{year}.png` (all `ExternalApi` output-cached), member-only download at `/account/badges/umbraco-mvp-{year}.png` (routes in `UseApplicationEndpoints`). No table: the assertion id is `base64url("{handle}:{year}" + HMAC)` signed with `OpenBadges:SigningKey` (a secret, ≥32 chars, Local.json / environment only; without it every endpoint 404s and the section is hidden; rotating it breaks issued badges). Recipient is the member's profile URL (type `url`); the MVP year runs June to June, so `issuedOn` = 1 June of the MVP year and `expires` = 1 June of the next year plus `OpenBadges:ExpiryGraceMonths` (default 1, i.e. 1 July); past years still resolve, as expired. Artwork is embedded `Features/OpenBadges/Assets/mvp-{year}.png`, resized to `OpenBadges:ImageSize` (400); a year without a file has no badge. See [`docs/tutorials/foundations/open-badges-mvp.md`](docs/tutorials/foundations/open-badges-mvp.md).

### Security Headers

Uses `Joonasw.AspNetCore.SecurityHeaders` for CSP and security headers:
- Custom CSP builder extensions in `Extensions/CspBuilderExtensions.cs`
- Nonce-based script security via `NonceTagHelper`
- CSP can be disabled per-request via `DisableCspMiddleware`
- HSTS enabled with preload and 1-year max age
- Permissions-Policy configured in `Extensions/WebApplicationExtensions.cs` (includes `clipboard-write=(self)` for share functionality, and delegates `autoplay`, `encrypted-media`, `fullscreen` and `picture-in-picture` to `www.youtube-nocookie.com` / `www.youtube.com`; without that, Chrome only lets YouTube embeds play muted)

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

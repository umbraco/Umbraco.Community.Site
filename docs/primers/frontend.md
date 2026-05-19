# Frontend primer

The public site's frontend lives in **`src/UmbracoCommunity.StaticAssets/`** — a Vite + TypeScript + Lit project that builds JS, CSS, and other static assets the Razor views in `UmbracoCommunity.Web.UI` consume at request time. This primer is an orientation doc: each section sketches what's there and links out to a tutorial, how-to, or source file for depth.

There are two *other* frontend codebases in the repo (`UmbracoCommunity.Extensions/Client/` and `UmbracoCommunity.BlockRestrictions/Client/`) for backoffice extensions. They're signposted at the end — they have their own Vite builds and aren't covered in detail here.

## Shape of things

The site renders as ASP.NET Razor views. Those views reference frontend assets built separately by Vite from `UmbracoCommunity.StaticAssets`:

```
src/
├── UmbracoCommunity.Web.UI/                  ← Razor views; consumes built assets
│   └── wwwroot/
│       └── assets/                            ← Vite's build output ships here for production
│           ├── manifest.json
│           ├── _index-abc123.js
│           └── _index-def456.css
├── UmbracoCommunity.StaticAssets/             ← The Vite project
│   ├── src/                                   ← Frontend source code
│   ├── dist/                                  ← Vite's build output before deploy
│   ├── vite.config.ts
│   └── package.json
└── UmbracoCommunity.Web/
    └── Vite/
        └── TagHelpers/                        ← C# bridge between Razor and Vite's manifest
```

You write code in `StaticAssets/src/`. `npm run build` produces hashed JS/CSS in `dist/`. A deploy step (or `npm run build:for:cloud`) copies those into `Web.UI/wwwroot/assets/`. Razor reads them via two TagHelpers that translate friendly names (`vite-src="index"`) into hashed filenames at request time.

## Dual dev workflow

Frontend dev needs two terminals running side by side:

```bash
# Terminal 1 — backend
cd src/UmbracoCommunity.Web.UI
dotnet run

# Terminal 2 — frontend
cd src/UmbracoCommunity.StaticAssets
npm run dev
```

The Vite dev server runs on **`https://localhost:5123`** with HTTPS via the `mkcert` Vite plugin — Vite's HMR client needs to match the page origin's protocol, and Umbraco runs HTTPS by default. First-run mkcert installs a local CA; if your browser doesn't trust the cert, accept the warning once.

In dev, the Vite TagHelpers rewrite asset URLs to point at the Vite dev server instead of `wwwroot/`. Edits to `.ts` or `.css` files trigger HMR in the browser. You'll have both terminals open whenever you're touching frontend code.

If you skip `npm run dev` and load the site, the TagHelpers will still emit dev-server URLs (because the C# environment is `Development`) but those URLs will 404 because Vite isn't serving them. Symptom: every script and stylesheet broken until you start the second terminal.

## How Vite reaches Razor

Two TagHelpers in [`UmbracoCommunity.Web/Vite/TagHelpers/`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/) bridge the gap between Vite's manifest and Razor:

- **`<script vite-src="index">`** emits a `<script type="module" src="…">`. In dev, `src` points at the Vite dev server (`https://localhost:5123/src/entrypoints/_index.ts`). In production, the TagHelper reads `wwwroot/assets/manifest.json`, looks up the `index` entry, and emits the hashed filename.
- **`<link rel="stylesheet" vite-href="index">`** is the same idea for CSS. In dev it's suppressed entirely — the JS chunk loads its own CSS via Vite's HMR. In production it resolves to `wwwroot/assets/_index-def456.css` plus any additional CSS files the manifest lists for that entry.

A third attribute, **`vite-client="true"`**, emits the `@vite/client` script that Vite needs for HMR. It's set on exactly one `<script>` per page (in `Layout.cshtml`) to avoid duplicating it.

The manifest is cached in memory and invalidated via `IFileProvider.Watch`, so a deploy that swaps `manifest.json` invalidates the cache without restarting the app. The lookup logic lives in [`ViteTagHelperBase.cs`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteTagHelperBase.cs); the per-asset behaviour is in [`ViteScriptTagHelper.cs`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteScriptTagHelper.cs) and [`ViteLinkTagHelper.cs`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteLinkTagHelper.cs).

→ A deeper tutorial on the dev/prod handover (and what to do when it breaks) is listed in [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md) under `vite-umbraco-manifest-integration`.

## Entry points

Every file in **`src/entrypoints/`** whose name starts with `_` is a Vite entry point. The config globs them in:

```ts
input: glob.sync("src/entrypoints/_*.ts").map((file) => file)
```

Current entries are `_index.ts` (the shared bundle loaded from `Layout.cshtml` on every page), `_home.ts`, and `_blog.ts`. Adding a new page-specific bundle is a matter of creating `src/entrypoints/_<alias>.ts` and referencing it from a Razor view.

`Layout.cshtml` resolves the per-page bundle dynamically off the current page's content type alias:

```cshtml
<link rel="stylesheet" vite-href="@currentPage.ContentTypeAlias" />
<script vite-src="@currentPage.ContentTypeAlias" asp-add-nonce="true"></script>
```

If a content type doesn't have a matching entrypoint, the TagHelper silently suppresses the tag — you don't have to create entrypoints for every page type, only the ones that need page-specific code.

## Where things live

Under `StaticAssets/src/`:

| Folder | What's in it |
| --- | --- |
| `entrypoints/` | Top-level Vite entries (`_*.ts`) — one shared bundle plus one per page type that needs custom JS/CSS |
| `components/` | Lit web components, including `sessionize/`, `form/`, `image-slider/`, and the slider family |
| `css/` | PostCSS stylesheets, organised by area (`base/`, `layout/`, `blocks/`, `typography.css`, …) |
| `services/` | Frontend services (fetch wrappers, logging, Sessionize, project/user services) |
| `integrations/` | Third-party integrations (Cookiebot, Intercom, Matomo, Google Maps) |
| `types/` | TypeScript ambient declarations and shared type aliases |
| `util/` | Pure utility functions — covered by the 80% coverage thresholds |
| `assets/` | Static binary assets (images, map pins) imported through TS |
| `svg/` | SVG icon modules — note that SVGs from Umbraco media are inlined server-side via the [`<svg-src>` TagHelper](../tutorials/foundations/inline-svg-tag-helper.md), not from here |
| `plugins/` | Vite plugins local to this project |
| `test/` | Test setup (`setup.ts`) and shared test helpers |

## Lit + PostCSS

Components are **Lit 3.x** web components. Conventions:

- Component file naming is `*.element.ts` (e.g. `dc-slider.element.ts`).
- Tests are colocated as `*.test.ts` next to the file they cover.
- RxJS is used where reactive streams help; Zod is used at API boundaries for runtime validation.

CSS is **PostCSS**. The pipeline lives in [`vite.config.ts`](../../src/UmbracoCommunity.StaticAssets/vite.config.ts) and combines three plugins:

- **`postcss-mixins`** with a custom `rhythm` mixin (see [`postcss-rhythm.mixin.ts`](../../src/UmbracoCommunity.StaticAssets/postcss-rhythm.mixin.ts)) — generates utility spacing classes like `.pt-md`, `.mx-xs`, `.m-lg` from CSS custom properties. Modifiers: `-xxs`, `-xs`, `-sm`, *(no suffix)*, `-md`, `-lg`, `-xl`, `-0`.
- **`postcss-calc`** — pre-computes `calc()` expressions at build time where it can.
- **`postcss-preset-env`** — opts in to upcoming CSS syntax (nesting, color functions, etc.) and inlines custom-property defaults.

→ A tutorial on the rhythm mixin pattern is listed in [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md) under `postcss-mixin-for-design-tokens`.

## Testing

Tests use **Vitest** with `jsdom`, configured in [`vitest.config.ts`](../../src/UmbracoCommunity.StaticAssets/vitest.config.ts). Conventions:

- Test files match `src/**/*.{test,spec}.ts` — colocated next to the file they cover.
- Coverage thresholds are 80% (branches, functions, lines, statements). The coverage-included paths are `util/`, `components/`, and `services/`.
- Setup runs from `src/test/setup.ts` — global mocks live there.

```bash
npm run test           # one-shot run
npm run test:ui        # interactive Vitest UI
npm run test:coverage  # with HTML coverage report
```

## Production build

`npm run build` runs **`tsc -p tsconfig.build.json`** (a strict type-check pass that fails the build on TS errors) followed by **`vite build`** (which emits hashed JS/CSS and `manifest.json` into `dist/`).

For Umbraco Cloud deploys, **`npm run build:for:cloud`** runs the build and then `node ./devops/copy-for-cloud.js` to copy the built assets into the Web.UI project so they ship with the deployment package. Self-hosted deploys handle the same copy through whatever mechanism your pipeline uses.

The `errorpage[extname]` Rollup output rule in `vite.config.ts` is a small nod to ASP.NET's error-page conventions: anything with `errorpage` in the name gets a stable (un-hashed) filename so the error page can reference it without round-tripping through the manifest.

## Other frontend codebases

Worth knowing they exist; not covered in detail here:

- **[`src/UmbracoCommunity.Extensions/Client/`](../../src/UmbracoCommunity.Extensions/Client/)** — a separate Vite + TypeScript project that builds custom backoffice extensions (custom dashboards, blog article creation flow). Outputs to `wwwroot/App_Plugins/UmbracoCommunityExtensions/`. Its own `package.json` and `vite.config.ts`.
- **[`src/UmbracoCommunity.BlockRestrictions/Client/`](../../src/UmbracoCommunity.BlockRestrictions/Client/)** — same shape for the Block Restrictions backoffice UI (custom property editors, workspace views, dashboards). Outputs to its own App_Plugin folder.

They're separate because the Umbraco backoffice has different module-loading conventions and runs inside its own design system. Mixing them with the public-site build would have meant ugly config gymnastics. Each backoffice client is small enough to own its own toolchain.

## Related how-to docs

When you go from "I understand how this hangs together" to "I want to add a thing":

- **[`docs/BUILDING_PAGES.md`](../BUILDING_PAGES.md)** — adding a new page type end to end. The frontend bit is creating a new `_<alias>.ts` entrypoint that gets picked up automatically by `Layout.cshtml`.
- **[`docs/BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md)** — adding a new content block. Touches `components/` if the block has interactive behaviour.

For *why* specific bits are shaped the way they are — the SVG TagHelper, the slider components, the form-steps progressive enhancement — see the [tutorials suite](../tutorials/README.md).

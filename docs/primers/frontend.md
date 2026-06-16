---
tags: [primer, frontend, architecture, vite, lit]
---

# Frontend primer

The public site's frontend lives in **`src/UmbracoCommunity.StaticAssets/`** — a Vite + TypeScript + Lit project whose job is to build the JS, CSS, and other static assets that the Razor views in `UmbracoCommunity.Web.UI` load at request time. The aim of this primer is to give you the lay of the land — each section sketches what's there and signposts a tutorial, how-to, or source file for when you want to dig in.

There are two *other* frontend codebases in the repo for backoffice extensions (`UmbracoCommunity.Extensions/Client/` and `UmbracoCommunity.BlockRestrictions/Client/`). They have their own Vite builds and we'll touch on them briefly at the end, but they're not the focus here.

> Just looking for the folder map? Skip to [Where things live](#where-things-live).

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

You write code in `StaticAssets/src/`. `npm run build` produces hashed JS/CSS in `dist/`. A deploy step (or `npm run build:for:cloud`) copies those into `Web.UI/wwwroot/assets/`. Razor reads them via two TagHelpers (Razor extensions that look like HTML elements but get expanded server-side before rendering) that translate friendly names (`vite-src="index"`) into hashed filenames at request time.

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

In dev, the Vite TagHelpers rewrite asset URLs to point at the Vite dev server instead of `wwwroot/`. Edits to `.ts` or `.css` files trigger HMR in the browser. You'll want both terminals open whenever you're touching frontend code.

A small gotcha worth knowing about up front: if you skip `npm run dev` and load the site, the TagHelpers will still emit dev-server URLs (because the C# environment is `Development`) but those URLs will 404 because Vite isn't there to serve them. The symptom is that every script and stylesheet on the page breaks — and the cure is just to start the second terminal.

## How Vite reaches Razor

The bit worth pausing on is *how* Vite hands its built filenames over to Razor. In dev mode we want HMR-friendly URLs that point at the Vite server; in production we want the hashed filename for the right asset off the manifest. Two TagHelpers in [`UmbracoCommunity.Web/Vite/TagHelpers/`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/) bridge that gap between Vite's manifest and Razor:

- **`<script vite-src="index">`** emits a `<script type="module" src="…">`. In dev, `src` points at the Vite dev server (`https://localhost:5123/src/entrypoints/_index.ts`). In production, the TagHelper reads `wwwroot/assets/manifest.json`, looks up the `index` entry, and emits the hashed filename.
- **`<link rel="stylesheet" vite-href="index">`** is the same idea for CSS. In dev it's suppressed entirely — the JS chunk loads its own CSS via Vite's HMR. In production it resolves to `wwwroot/assets/_index-def456.css` plus any additional CSS files the manifest lists for that entry.

A third attribute, **`vite-client="true"`**, emits the `@vite/client` script that Vite needs for HMR. It's set on exactly one `<script>` per page (in `Layout.cshtml`) to avoid duplicating it.

The manifest is cached in memory and invalidated via `IFileProvider.Watch`, so a deploy that swaps `manifest.json` invalidates the cache without restarting the app. The lookup logic lives in [`ViteTagHelperBase.cs`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteTagHelperBase.cs); the per-asset behaviour is in [`ViteScriptTagHelper.cs`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteScriptTagHelper.cs) and [`ViteLinkTagHelper.cs`](../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteLinkTagHelper.cs).

→ A deeper tutorial on the dev/prod handover (and what to do when it breaks) is planned — see [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md).

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
| `integrations/` | Third-party integrations — Cookiebot consent, plus a `script-loader` base element (Intercom/Matomo/Maps are *not* wired up here despite older mentions) |
| `types/` | TypeScript ambient declarations and shared type aliases |
| `util/` | Pure utility functions — covered by the 80% coverage thresholds |
| `assets/` | Static binary assets (images, map pins) imported through TS |
| `svg/` | SVG icon modules — note that SVGs from Umbraco media are inlined server-side via the [`<svg-src>` TagHelper](../tutorials/foundations/inline-svg-tag-helper.md), not from here |
| `plugins/` | Vite plugins local to this project |
| `test/` | Test setup (`setup.ts`) and shared test helpers |

## Lit + PostCSS

Components on the public site are **Lit 3.x** web components — [Lit](https://lit.dev) is a small (~6 KB) library for authoring standards-based custom elements with reactive properties and template literals, which gets us most of the developer ergonomics of a framework without the virtual DOM or any framework lock-in. A few conventions to bear in mind:

- Component file naming is `*.element.ts` (e.g. `dc-slider.element.ts`). The `dc-` prefix is a project naming convention with no special meaning.
- Tests are colocated as `*.test.ts` next to the file they cover.
- [RxJS](https://rxjs.dev) (Observable streams) is used where reactive composition of async data helps; [Zod](https://zod.dev) is used at API boundaries for runtime validation of fetched JSON.

The smallest possible component:

```ts
import { html, LitElement } from "lit";
import { customElement } from "lit/decorators.js";

@customElement("dc-hello")
export class DcHello extends LitElement {
  render() { return html`<p>Hello</p>`; }
}
```

Drop that in `src/components/dc-hello.element.ts`, import it from an entrypoint, and `<dc-hello></dc-hello>` works in any Razor view.

CSS is **[PostCSS](https://postcss.org)** — a CSS post-processor pipeline (think Sass-style features, but the input is standard CSS plus opt-in next-gen syntax). The pipeline lives in [`vite.config.ts`](../../src/UmbracoCommunity.StaticAssets/vite.config.ts) and combines three plugins:

- **`postcss-mixins`** with a custom `rhythm` mixin (see [`postcss-rhythm.mixin.ts`](../../src/UmbracoCommunity.StaticAssets/postcss-rhythm.mixin.ts)) — generates utility spacing classes like `.pt-md`, `.mx-xs`, `.m-lg` from CSS custom properties defined once in `root.css`. The point is design-token-driven spacing: one source of truth for the rem scale, used by hundreds of layouts, so spacing stays consistent without anyone having to remember individual values. Modifiers: `-xxs`, `-xs`, `-sm`, *(no suffix)*, `-md`, `-lg`, `-xl`, `-0`.
- **`postcss-calc`** — pre-computes `calc()` expressions at build time where it can.
- **`postcss-preset-env`** — opts in to upcoming CSS syntax (nesting, color functions, etc.) and inlines custom-property defaults.

→ A tutorial on the rhythm mixin pattern is planned — see [`docs/tutorials/IDEAS.md`](../tutorials/IDEAS.md).

## Testing

Tests use **[Vitest](https://vitest.dev)** with `jsdom`, configured in [`vitest.config.ts`](../../src/UmbracoCommunity.StaticAssets/vitest.config.ts). Conventions:

- Test files match `src/**/*.{test,spec}.ts` — colocated next to the file they cover.
- Coverage thresholds are 80% (branches, functions, lines, statements) across `util/`, `components/`, and `services/`; falling below them fails `npm run test:coverage` locally and CI.
- Setup runs from `src/test/setup.ts` — global mocks live there.

```bash
npm run test           # one-shot run
npm run test:ui        # interactive Vitest UI
npm run test:coverage  # with HTML coverage report
```

## Production build

`npm run build` runs **`tsc -p tsconfig.build.json`** (a strict type-check pass that fails the build on TS errors) followed by **`vite build`** (which emits hashed JS/CSS and `manifest.json` into `dist/`).

For Umbraco Cloud deploys, **`npm run build:for:cloud`** runs the build and then `node ./devops/copy-for-cloud.js` to copy the built assets into the Web.UI project so they ship with the deployment package. Self-hosted deploys handle the same copy through whatever mechanism your pipeline uses.

The `errorpage[extname]` rule in `vite.config.ts` (Vite uses Rollup under the hood for its output config) is a small nod to ASP.NET's error-page conventions: anything with `errorpage` in the name gets a stable (un-hashed) filename so the error page can reference it without round-tripping through the manifest.

## Other frontend codebases

Worth knowing they exist; not covered in detail here:

- **[`src/UmbracoCommunity.Extensions/Client/`](../../src/UmbracoCommunity.Extensions/Client/)** — a separate Vite + TypeScript project that builds custom backoffice extensions (custom dashboards, blog article creation flow). Outputs to `wwwroot/App_Plugins/UmbracoCommunityExtensions/`. Its own `package.json` and `vite.config.ts`.
- **[`src/UmbracoCommunity.BlockRestrictions/Client/`](../../src/UmbracoCommunity.BlockRestrictions/Client/)** — same shape for the Block Restrictions backoffice UI (custom property editors, workspace views, dashboards). Outputs to its own App_Plugin folder.

They're separate because the Umbraco backoffice has different module-loading conventions and runs inside its own design system. Mixing them with the public-site build would have meant ugly config gymnastics. Each backoffice client is small enough to own its own toolchain.

## Related how-to docs

When you go from "I understand how this hangs together" to "I want to add a thing":

- **[`docs/BUILDING_PAGES.md`](../BUILDING_PAGES.md)** — adding a new page type end to end. The frontend bit is creating a new `_<alias>.ts` entrypoint that gets picked up automatically by `Layout.cshtml`.
- **[`docs/BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md)** — adding a new content block. Touches `components/` if the block has interactive behaviour.
- **[`CODE_CONVENTIONS.md`](../../CODE_CONVENTIONS.md)** — naming patterns including TypeScript component file names (`*.element.ts`) and the `dc-` web-component tag prefix used on the public site.
- **[`ACCESSIBILITY.md`](../../ACCESSIBILITY.md)** — WCAG 2.1 AA conformance notes: focus management, keyboard navigation, ARIA, the Lucide icon conventions. Worth reading before adding any interactive component.

For *why* specific bits are shaped the way they are — the SVG TagHelper, the slider components, the form-steps progressive enhancement — see the [tutorials suite](../tutorials/README.md). Each tutorial stands alone, so feel free to dip in wherever a problem you're hitting matches.

Hopefully that's enough to find your feet — happy building!

# Tutorials

Short, standalone tutorials covering specific problems we've hit on the Umbraco Community site and the approaches we took to solve them.

Each tutorial is self-contained: it states the concrete problem, explains the trade-offs of our solution, and walks through the implementation file by file. They're written to be picked up on a different Umbraco project (or no Umbraco project at all) — but they reference real code in this repo, so anyone working *on* the site can jump straight to the source if they prefer to read code over prose.

These sit alongside the other docs in this folder rather than replacing them:

- **[Primers](../primers/)** orient you to a whole area of the codebase (e.g. the frontend) with links out to the deeper docs for each topic. Start here if you're new.
- **How-to guides** like [`BUILDING_PAGES.md`](../BUILDING_PAGES.md) and [`BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md) tell you the conventions for adding new things to this codebase.
- **Operational notes** like [`LESSONS_LEARNED.md`](../LESSONS_LEARNED.md) cover workflow gotchas (Umbraco upgrades, cloud deploys, schema management).
- **Tutorials** *(this folder)* explain *why* a particular piece of the codebase is shaped the way it is, and how to build something similar from scratch.

## How to read these

Tutorials are split into two kinds:

- **`foundations/`** — pieces of code that other tutorials build on. Read these first if a refinement says it's a prerequisite.
- **`refinements/`** — extensions, bug fixes, or improvements layered onto a foundation. Each refinement names the foundation it depends on at the top.

You don't need to read the suite in order. Each tutorial points at the next logical stop in its closing section, so you can either follow a thread the whole way through, or just jump straight to whichever problem happens to match the one in front of you.

## What's here

### Foundations

- **[Building an inline SVG TagHelper for Umbraco](foundations/inline-svg-tag-helper.md)** — A custom `<svg-src>` TagHelper that reads SVG files from Umbraco media and inlines them into the page, so they can be styled and animated with CSS. Adapted from Warren Buckley's [Our.Umbraco.TagHelpers](https://github.com/umbraco-community/Our-Umbraco-TagHelpers).
- **[Resolving content in a multi-tenant Umbraco site](foundations/multi-tenant-content-resolution.md)** — One Umbraco instance, multiple tenant root content nodes. Walks through the `Root()` + `GetSiteSettings()` pattern that keeps every content lookup scoped to the current request's tenant, the document-tree shape it assumes, and the small set of helpers that consumers (menu, footer, SEO, sitemap) lean on.
- **[Wiring Vite's manifest into Umbraco's Razor pipeline](foundations/vite-umbraco-manifest-integration.md)** — A pair of TagHelpers (`<script vite-src>` / `<link vite-href>`) that point at the Vite dev server on `:5123` for HMR in development and at the content-hashed files named in `manifest.json` in production. Covers the entry-name convention, dev-vs-prod CSS handling, and `IFileProvider.Watch`-based cache invalidation that survives a deploy without an app restart.
- **[Progressive enhancement of async-rendered DOM with MutationObserver](foundations/mutation-observer-progressive-enhancement.md)** — Enhancing DOM a third-party widget renders on its own schedule. The `<dc-form-steps>` element waits for an Umbraco Forms form (async-rendered inside `<umb-forms-render>`) to appear, turns it into a multi-step form once it does, and disconnects the observer cleanly. The pattern transfers anywhere your enhancement might run before the thing it enhances exists.
- **[One master block data type, restricted per consumer](foundations/one-master-block-datatype.md)** — The content-modelling decision behind Block Restrictions: keep a single Block Grid/List data type holding every block, point all document types at it, and narrow the menu per document type with a rule — instead of a near-duplicate data type per document type (and per tenant). Frames the data-type-sprawl problem, the master-plus-rule alternative, and links to the resolution and editor-enforcement tutorials that implement it.

### Refinements

- **[Scoping inline SVG `<style>` to prevent class-name bleed](refinements/scoping-inline-svg-styles.md)** *(builds on the inline SVG TagHelper)* — Illustrator-exported SVGs ship `<style>` blocks with generic class names (`.st0`, `.st1`, …) that are document-scoped, not SVG-scoped. Two such SVGs on one page fight over the same class names. Fix: have the TagHelper add a deterministic class per SVG file and prefix every internal selector with it.
- **[Caching the scoped SVG output](refinements/caching-scoped-svg-output.md)** *(builds on the scoping refinement)* — Once the scoped SVG markup is deterministic per media path, every render of the same SVG is byte-identical. Wrap the read + sanitise + parse + scope work in Umbraco's `RuntimeCache` keyed by media path; serve the cached blob directly on the hot path. Skips the cloud media round-trip entirely after warm-up.
- **[Per-tenant 404 pages with `IContentLastChanceFinder`](refinements/per-tenant-404-content-finder.md)** *(builds on multi-tenant content resolution)* — When the request 404'd there's no current page to anchor tenant lookups off. Resolve the tenant root from the domain binding instead (`request.Domain?.ContentId`), walk descendants for a `PageNotFound` content type, and hand it back to Umbraco's router with HTTP status 404.
- **[Tenant-aware fallback for schema and SEO metadata](refinements/tenant-fallback-for-schema-and-seo.md)** *(builds on multi-tenant content resolution)* — Every page emits `Organization` schema, but editors forget to fill in tenant brand fields. A small `OrganizationSchemaBuilder` accepts a nullable `SocialSettings` and falls back to hardcoded constants so unconfigured tenants still produce valid schema.
- **[Wrapping Umbraco's native block editor with restriction filtering](refinements/wrapping-umbraco-native-block-editor.md)** — A custom property editor UI that reuses the native Block Grid/List *schema* but wraps the native *element*, filtering its allowed-block list down to a tree-resolved restriction. Covers Light-DOM context propagation, the recreate-on-restriction pattern when the rule arrives late, the Grid-vs-List filtering asymmetry, and the clipboard value translators that copy/paste needs to survive the wrapping. Compares the wrap approach against the server-side modal-replacement approach of Kraftvaerk.Umbraco.BlockFilter.

### Planned

The backlog in [`IDEAS.md`](./IDEAS.md) lists tutorials that haven't been written yet. Each idea there also has a placeholder file under `foundations/` or `refinements/` with a status callout and a "what this will cover" sketch — useful if you're picking one up to write, or just want to scan what's coming without reading the backlog index.

## Contributing a new tutorial

When adding a new tutorial:

1. Check the [backlog](./IDEAS.md) first. If your topic is already there, it has a placeholder file under `foundations/` or `refinements/` — expand the stub in place rather than creating a duplicate. If your topic isn't listed yet, that's fine; just pick the right folder for it (foundations stand alone; refinements depend on an earlier piece of code) and create a new file with a kebab-case filename that describes the *technique* rather than the bug.
2. Follow the section structure used by the existing tutorials: a one-paragraph framing, then **The problem** → **Why the obvious fix doesn't work** → **Our approach** → **Walkthrough** → **Alternatives we considered** → **Trade-offs and known limits**. For foundation pieces, swap "The problem" for **Why you might want this** and "Why the obvious fix doesn't work" for **What we're building**.
3. Link to real files in this repo using paths relative to the tutorial file (e.g. `../../../src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`).
4. Credit prior art. If the code is adapted from a community project, lead with a "Credit where it's due" section linking the source and naming contributors.
5. Move the tutorial out of "planned" and into the relevant section above: add it to **What's here**, and update its backlog entry in [`IDEAS.md`](./IDEAS.md) (either strike it through with a "shipped as ..." note, or remove the bullet entirely) — all in the same commit.

You don't need to maintain a contributors list by hand. Each rendered doc shows a **Contributors** section generated from git history (`docs/contributors.generated.json`, produced by `npm run generate:doc-contributors` and refreshed in CI). Open a PR and you'll be credited automatically — with your GitHub avatar where your commit email is linked to your account.

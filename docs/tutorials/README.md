# Tutorials

Short, standalone tutorials covering specific problems we've hit on the Umbraco Community site and the approaches we took to solve them.

Each tutorial is self-contained: it states the concrete problem, explains the trade-offs of our solution, and walks through the implementation file by file. They're written so a developer on a different Umbraco project (or no Umbraco project at all) can pick up the pattern, but they reference real code in this repo so anyone working *on* this site can jump straight to the source.

These sit alongside the other docs in this folder rather than replacing them:

- **How-to guides** like [`BUILDING_PAGES.md`](../BUILDING_PAGES.md) and [`BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md) tell you the conventions for adding new things to this codebase.
- **Operational notes** like [`LESSONS_LEARNED.md`](../LESSONS_LEARNED.md) cover workflow gotchas (Umbraco upgrades, cloud deploys, schema management).
- **Tutorials** *(this folder)* explain *why* a particular piece of the codebase is shaped the way it is, and how to build something similar from scratch.

## How to read these

Tutorials are split into two kinds:

- **`foundations/`** — pieces of code that other tutorials build on. Read these first if a refinement says it's a prerequisite.
- **`refinements/`** — extensions, bug fixes, or improvements layered onto a foundation. Each refinement names the foundation it depends on at the top.

You don't need to read the suite in order. Each tutorial points at the next logical stop in its closing section, so you can follow a thread, or jump straight to whichever problem matches the one in front of you.

## What's here

### Foundations

- **[Building an inline SVG TagHelper for Umbraco](foundations/inline-svg-tag-helper.md)** — A custom `<svg-src>` TagHelper that reads SVG files from Umbraco media and inlines them into the page, so they can be styled and animated with CSS. Adapted from Warren Buckley's [Our.Umbraco.TagHelpers](https://github.com/umbraco-community/Our-Umbraco-TagHelpers).
- **[Resolving content in a multi-tenant Umbraco site](foundations/multi-tenant-content-resolution.md)** — One Umbraco instance, multiple tenant root content nodes. Walks through the `Root()` + `GetSiteSettings()` pattern that keeps every content lookup scoped to the current request's tenant, the document-tree shape it assumes, and the small set of helpers that consumers (menu, footer, SEO, sitemap) lean on.

### Refinements

- **[Scoping inline SVG `<style>` to prevent class-name bleed](refinements/scoping-inline-svg-styles.md)** *(builds on the inline SVG TagHelper)* — Illustrator-exported SVGs ship `<style>` blocks with generic class names (`.st0`, `.st1`, …) that are document-scoped, not SVG-scoped. Two such SVGs on one page fight over the same class names. Fix: have the TagHelper add a deterministic class per SVG file and prefix every internal selector with it.
- **[Caching the scoped SVG output](refinements/caching-scoped-svg-output.md)** *(builds on the scoping refinement)* — Once the scoped SVG markup is deterministic per media path, every render of the same SVG is byte-identical. Wrap the read + sanitise + parse + scope work in Umbraco's `RuntimeCache` keyed by media path; serve the cached blob directly on the hot path. Skips the cloud media round-trip entirely after warm-up.
- **[Per-tenant 404 pages with `IContentLastChanceFinder`](refinements/per-tenant-404-content-finder.md)** *(builds on multi-tenant content resolution)* — When the request 404'd there's no current page to anchor tenant lookups off. Resolve the tenant root from the domain binding instead (`request.Domain?.ContentId`), walk descendants for a `PageNotFound` content type, and hand it back to Umbraco's router with HTTP status 404.

## Contributing a new tutorial

When adding a new tutorial:

1. Pick the right folder — does it stand alone, or does it require an earlier piece of code? Foundations live in `foundations/`; anything that says *"first you need X"* belongs in `refinements/`.
2. Use kebab-case filenames that describe the *technique*, not the bug (so the file is findable later as a "how do I do X" reference, not just "the time we broke Y").
3. Follow the section structure used by the existing tutorials: a one-paragraph framing, then **The problem** → **Why the obvious fix doesn't work** → **Our approach** → **Walkthrough** → **Alternatives we considered** → **Trade-offs and known limits**. For foundation pieces, swap "The problem" for **Why you might want this** and "Why the obvious fix doesn't work" for **What we're building**.
4. Link to real files in this repo using paths relative to the tutorial file (e.g. `../../../src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`).
5. Credit prior art. If the code is adapted from a community project, lead with a "Credit where it's due" section linking the source and naming contributors.
6. Add the tutorial to the list above.

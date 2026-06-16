# Tutorial ideas

A backlog of candidate topics for future tutorials in this folder. Each entry frames the *problem* the tutorial would tackle and what makes it worth writing up — the non-obvious bit, the trade-off, or the pattern that transfers beyond this repo. Prune entries that stop seeming interesting; add new ones as you hit them.

These don't follow the tutorial section template — that's for the actual write-ups. Here we just want enough to remember why each idea felt worth keeping.

## Top picks

If you only write three more, these are the ones with the strongest "non-obvious problem, broad audience" shape:

- ~~**`multi-tenant-content-resolution`**~~ — shipped as [foundations/multi-tenant-content-resolution.md](./foundations/multi-tenant-content-resolution.md).
- ~~**`vite-umbraco-manifest-integration`**~~ — shipped as [foundations/vite-umbraco-manifest-integration.md](./foundations/vite-umbraco-manifest-integration.md).
- ~~**`mutation-observer-progressive-enhancement`**~~ — shipped as [foundations/mutation-observer-progressive-enhancement.md](./foundations/mutation-observer-progressive-enhancement.md).

## Foundations

Reusable primitives that other tutorials can build on.

- ~~**`one-master-block-datatype`**~~ — shipped as [foundations/one-master-block-datatype.md](./foundations/one-master-block-datatype.md). The content-modelling *why* behind Block Restrictions (one master block data type narrowed per document type vs. a data type per document type); the motivating entry point for the inherited-config foundation and the editor-wrapping refinement.
- ~~**`multi-tenant-content-resolution`**~~ — shipped (see top picks).
- ~~**`vite-umbraco-manifest-integration`**~~ — shipped as [foundations/vite-umbraco-manifest-integration.md](./foundations/vite-umbraco-manifest-integration.md). Foundation for anything else frontend-related.
- **`intersection-observer-paused-animation`** ([stub →](./foundations/intersection-observer-paused-animation.md)) — the `<dc-image-slider>` auto-scroll. `requestAnimationFrame` for the loop, `IntersectionObserver` to pause when off-screen, `visibilitychange` for tab switches, `prefers-reduced-motion` for accessibility. The general lesson: how to animate something cheaply *and* politely without leaning on a library.
- **`drag-to-scroll-with-snap`** ([stub →](./foundations/drag-to-scroll-with-snap.md)) — the `<dc-slider>` component. Touch drag follows finger and snaps on release; desktop gets hover-zone navigation; explicit arrow buttons are an opt-in. Building a usable scroller in vanilla web components instead of pulling in Swiper / Embla.
- ~~**`content-tree-inherited-config`**~~ — shipped as [foundations/content-tree-inherited-config.md](./foundations/content-tree-inherited-config.md).
- ~~**`mutation-observer-progressive-enhancement`**~~ — shipped as [foundations/mutation-observer-progressive-enhancement.md](./foundations/mutation-observer-progressive-enhancement.md).
- **`postcss-mixin-for-design-tokens`** ([stub →](./foundations/postcss-mixin-for-design-tokens.md)) — the rhythm mixin. Generates `.pt-md`, `.mx-xs`, `.m-lg`, etc. from CSS custom properties at build time. Foundation for "I want utility classes but driven by my design system, not Tailwind." Worth pairing with a tiny section on the CSS media query grouping convention (already in memory).
- **`nonce-csp-with-razor`** ([stub →](./foundations/nonce-csp-with-razor.md)) — the `NonceTagHelper` + Joonasw integration + per-request `DisableCspMiddleware` escape hatch. Building a strict CSP that doesn't make inline scripts impossible in ASP.NET Core. CSP-in-.NET is poorly documented; this would be the post Laura wishes had existed.
- **`site-search-with-examine-externalindex`** ([stub →](./foundations/site-search-with-examine-externalindex.md)) — wiring the nav search icon to a `SearchPage` doc type backed by Umbraco's `ExternalIndex` (`SearchService`, `SearchPageController`, `SearchPageViewModel`). The non-obvious bits: scoping results to the current tenant's root so multi-tenant sites don't bleed hits across brands, stripping HTML out of excerpts before display, and the trade-off between querying Examine in the controller vs. via a typed service. "How do I add search to my Umbraco site" is a perennial question and most answers stop at the single-tenant happy path.
- **`backoffice-management-api-with-auth-policies`** ([stub →](./foundations/backoffice-management-api-with-auth-policies.md)) — the `BlockRestrictionApiController` pattern: routing under `/umbraco/.../api/v1`, `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]` for backoffice-only endpoints, Swagger doc registration, and a typed fetch wrapper on the client that pulls the user's backoffice bearer token. Community content on the new backoffice almost exclusively covers the property-editor UI; the secured C# side that the UI actually calls is consistently under-documented.

## Refinements

Improvements layered on top of a foundation, in this repo or otherwise.

- **`dual-persistence-db-and-json`** ([stub →](./refinements/dual-persistence-db-and-json.md)) *(builds on content-tree-inherited config, or stands alone)* — Block Restrictions stores rules in both EF Core and JSON files so they're version-controllable. Plus zip export/import for Umbraco Cloud environments where you can't ship files via deployment. The non-obvious bit: how to keep DB and disk in sync without an infinite save loop and how to make the JSON the source of truth on first boot.
- ~~**`wrapping-umbraco-native-block-editor`**~~ — shipped as [refinements/wrapping-umbraco-native-block-editor.md](./refinements/wrapping-umbraco-native-block-editor.md).
- **`shipping-ef-core-migrations-from-a-package`** ([stub →](./refinements/shipping-ef-core-migrations-from-a-package.md)) — the Block Restrictions Razor Class Library owns its own DbContext and migrations. A composer registers a hosted service that runs migrations *after* Umbraco has booted (PR #132 on develop is the fix for getting that ordering right). Pattern: "I want my package to own its schema without making the host wire anything up."
- ~~**`custom-content-finder-for-per-tenant-404`**~~ — shipped as [refinements/per-tenant-404-content-finder.md](./refinements/per-tenant-404-content-finder.md).
- **`closest-host-opt-in-for-shared-component`** ([stub →](./refinements/closest-host-opt-in-for-shared-component.md)) *(builds on drag-to-scroll-with-snap)* — `<dc-slider>` is reused by both the slider block and the blog showcase block. Instead of duplicating the component or adding a host-type parameter, it uses `closest('.dc-slider-block, .dc-blog-showcase-block')` plus an opt-in `has-buttons` class on the ancestor. Pattern: how to let one web component serve multiple host contexts without parameter explosion.
- ~~**`schema-net-with-tenant-fallback`**~~ — shipped as [refinements/tenant-fallback-for-schema-and-seo.md](./refinements/tenant-fallback-for-schema-and-seo.md).
- **`sessionize-question-answer-resolution`** ([stub →](./refinements/sessionize-question-answer-resolution.md)) — extracting structured-but-arbitrary data from an external API. Sessionize stores pronouns as a free-form Q&A entry; you have to find the right question ID once (cached on `SessionizeAllData.PronounsQuestionId`) and then look up each speaker's matching answer. Reusable wherever you've got "answers keyed by question ID" data and don't want to look up the question per item.
- **`output-cache-policies-for-slow-upstream-apis`** ([stub →](./refinements/output-cache-policies-for-slow-upstream-apis.md)) *(stands alone or pairs with the Sessionize question-answer tutorial)* — the `OutputCachePolicies` class wrapping the Sessionize endpoints. When `[OutputCache]` beats `ResponseCaching` (you control the key, you can vary by query, it survives across instances with a distributed store), what cache-key shapes make sense for tenant-scoped data, and how to fail gracefully when the upstream is rate-limited or 500s. The general lesson: how to be a polite consumer of a slow third-party API without making *your* page slow.

## How to use this list

Every idea above has a placeholder file linked inline (the `[stub →]` link next to the slug). When you pick one to write:

1. Open the stub file and replace its scaffolding with the full tutorial — same path, same filename, no new file to create.
2. Move the bullet from here to the "What's here" section in [`README.md`](./README.md) in the same commit; either strike the bullet through with a `— shipped as [path](path)` note (the convention the other shipped entries use) or remove it outright.
3. If the tutorial unlocks a new candidate (e.g. "this would be even better with a follow-up on X"), add the new idea to this file *and* create a fresh stub file alongside it in the same commit so the two stay in lockstep.

The brainstorm shouldn't get stale. If an entry has been sitting here for six months and you've never reached for it, that's a signal to delete both the bullet and its stub rather than to feel guilty about it.

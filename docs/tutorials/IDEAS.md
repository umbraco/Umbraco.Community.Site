# Tutorial ideas

A backlog of candidate topics for future tutorials in this folder. Each entry frames the *problem* the tutorial would tackle and what makes it worth writing up — the non-obvious bit, the trade-off, or the pattern that transfers beyond this repo. Prune entries that stop seeming interesting; add new ones as you hit them.

These don't follow the tutorial section template — that's for the actual write-ups. Here we just want enough to remember why each idea felt worth keeping.

## Top picks

If you only write three more, these are the ones with the strongest "non-obvious problem, broad audience" shape:

- ~~**`multi-tenant-content-resolution`**~~ — shipped as [foundations/multi-tenant-content-resolution.md](./foundations/multi-tenant-content-resolution.md).
- **`vite-umbraco-manifest-integration`** — modern frontend tooling talking to a server-rendered Razor view. Dev mode hits the Vite dev server on `:5123` for HMR; prod mode reads `manifest.json` and emits the right `<script>`/`<link>` tags via a TagHelper. Audience is much wider than this repo — every "I want Vite/React/Lit in my .NET project" tutorial online either skips this or gets it wrong.
- **`mutation-observer-progressive-enhancement`** — the `<dc-form-steps>` problem: your enhancement runs before the thing it enhances exists. Umbraco Forms async-renders inside `<umb-forms-render>`, so the steps component uses a MutationObserver to wait for the field groups to appear, then converts them to steps. The pattern transfers anywhere you're enhancing async-rendered DOM.

## Foundations

Reusable primitives that other tutorials can build on.

- ~~**`multi-tenant-content-resolution`**~~ — shipped (see top picks).
- **`vite-umbraco-manifest-integration`** — see top picks. Foundation for the dual-build refinement below and anything else frontend-related.
- **`intersection-observer-paused-animation`** — the `<dc-image-slider>` auto-scroll. `requestAnimationFrame` for the loop, `IntersectionObserver` to pause when off-screen, `visibilitychange` for tab switches, `prefers-reduced-motion` for accessibility. The general lesson: how to animate something cheaply *and* politely without leaning on a library.
- **`drag-to-scroll-with-snap`** — the `<dc-slider>` component. Touch drag follows finger and snaps on release; desktop gets hover-zone navigation; explicit arrow buttons are an opt-in. Building a usable scroller in vanilla web components instead of pulling in Swiper / Embla.
- **`content-tree-inherited-config`** — Block Restrictions' core idea, but framed generally: attach rules to a doc type, let descendants inherit by ancestor walk, cache the resolution, fail open when nothing is configured. The pattern shows up anywhere you have "configure on a parent, apply on descendants" (CMS perms, feature flags by section, theming overrides).
- **`mutation-observer-progressive-enhancement`** — see top picks.
- **`postcss-mixin-for-design-tokens`** — the rhythm mixin. Generates `.pt-md`, `.mx-xs`, `.m-lg`, etc. from CSS custom properties at build time. Foundation for "I want utility classes but driven by my design system, not Tailwind." Worth pairing with a tiny section on the CSS media query grouping convention (already in memory).
- **`nonce-csp-with-razor`** — the `NonceTagHelper` + Joonasw integration + per-request `DisableCspMiddleware` escape hatch. Building a strict CSP that doesn't make inline scripts impossible in ASP.NET Core. CSP-in-.NET is poorly documented; this would be the post Laura wishes had existed.
- **`site-search-with-examine-externalindex`** — wiring the nav search icon to a `SearchPage` doc type backed by Umbraco's `ExternalIndex` (`SearchService`, `SearchPageController`, `SearchPageViewModel`). The non-obvious bits: scoping results to the current tenant's root so multi-tenant sites don't bleed hits across brands, stripping HTML out of excerpts before display, and the trade-off between querying Examine in the controller vs. via a typed service. "How do I add search to my Umbraco site" is a perennial question and most answers stop at the single-tenant happy path.
- **`backoffice-management-api-with-auth-policies`** — the `BlockRestrictionApiController` pattern: routing under `/umbraco/.../api/v1`, `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]` for backoffice-only endpoints, Swagger doc registration, and a typed fetch wrapper on the client that pulls the user's backoffice bearer token. Community content on the new backoffice almost exclusively covers the property-editor UI; the secured C# side that the UI actually calls is consistently under-documented.

## Refinements

Improvements layered on top of a foundation, in this repo or otherwise.

- **`dual-build-frontend-and-backoffice`** *(builds on the Vite/Umbraco integration)* — same Vite project produces two bundles: the public site bundle and a backoffice bundle that lands in `App_Plugins/UmbracoCommunityGitHubUsers/`. `BUILD_TARGET=backoffice` switches entry points and output paths. Pattern: one toolchain serving two very different consumers.
- **`dual-persistence-db-and-json`** *(builds on content-tree-inherited config, or stands alone)* — Block Restrictions stores rules in both EF Core and JSON files so they're version-controllable. Plus zip export/import for Umbraco Cloud environments where you can't ship files via deployment. The non-obvious bit: how to keep DB and disk in sync without an infinite save loop and how to make the JSON the source of truth on first boot.
- **`wrapping-umbraco-native-block-editor`** — `BlockGridRestricted` and `BlockListRestricted` are custom property editor UIs that delegate to the native block editor while filtering its allowed-block list. Shows how to extend an existing backoffice editor without forking it, including the clipboard translator boilerplate so copy-paste survives the wrapping.
- **`shipping-ef-core-migrations-from-a-package`** — the Block Restrictions Razor Class Library owns its own DbContext and migrations. A composer registers a hosted service that runs migrations *after* Umbraco has booted (PR #132 on develop is the fix for getting that ordering right). Pattern: "I want my package to own its schema without making the host wire anything up."
- ~~**`custom-content-finder-for-per-tenant-404`**~~ — shipped as [refinements/per-tenant-404-content-finder.md](./refinements/per-tenant-404-content-finder.md).
- **`closest-host-opt-in-for-shared-component`** *(builds on drag-to-scroll-with-snap)* — `<dc-slider>` is reused by both the slider block and the blog showcase block. Instead of duplicating the component or adding a host-type parameter, it uses `closest('.dc-slider-block, .dc-blog-showcase-block')` plus an opt-in `has-buttons` class on the ancestor. Pattern: how to let one web component serve multiple host contexts without parameter explosion.
- ~~**`schema-net-with-tenant-fallback`**~~ — shipped as [refinements/tenant-fallback-for-schema-and-seo.md](./refinements/tenant-fallback-for-schema-and-seo.md).
- **`sessionize-question-answer-resolution`** — extracting structured-but-arbitrary data from an external API. Sessionize stores pronouns as a free-form Q&A entry; you have to find the right question ID once (cached on `SessionizeAllData.PronounsQuestionId`) and then look up each speaker's matching answer. Reusable wherever you've got "answers keyed by question ID" data and don't want to look up the question per item.
- **`output-cache-policies-for-slow-upstream-apis`** *(stands alone or pairs with the Sessionize question-answer tutorial)* — the `OutputCachePolicies` class wrapping the Sessionize endpoints. When `[OutputCache]` beats `ResponseCaching` (you control the key, you can vary by query, it survives across instances with a distributed store), what cache-key shapes make sense for tenant-scoped data, and how to fail gracefully when the upstream is rate-limited or 500s. The general lesson: how to be a polite consumer of a slow third-party API without making *your* page slow.

## How to use this list

When you pick one to write:

1. Move it from here to the "What's here" section in [`README.md`](./README.md) as part of the same commit that adds the tutorial file.
2. Either delete its entry here or leave a one-line note that it shipped — whichever feels less confusing later.
3. If the tutorial unlocks a new candidate (e.g. "this would be even better with a follow-up on X"), add the new idea to this file in the same commit.

The brainstorm shouldn't get stale. If an entry has been sitting here for six months and you've never reached for it, that's a signal to delete rather than to feel guilty about it.

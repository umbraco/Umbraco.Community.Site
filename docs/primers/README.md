# Primers

Concept-oriented overviews of how the major areas of this codebase hang together.

A primer doesn't tell you how to add a new thing (that's what the `BUILDING_*` how-to docs are for) and doesn't explain why one specific bit of code looks the way it does (that's tutorials). It gives you the **lay of the land** for an area, with links out to the deeper docs for each topic.

If you're new to the codebase, start here. If you've been here a while and someone asks "how does X work in our project", point them at the relevant primer.

## What's here

- **[Frontend primer](frontend.md)** — the Vite-powered public-site frontend in `UmbracoCommunity.StaticAssets`. Covers the dual dev workflow, the manifest-driven Razor integration, the entrypoint convention, Lit + PostCSS, testing, and what builds for production. Backoffice frontends are signposted at the end.
- **[Backend primer](backend.md)** — the C# side in `UmbracoCommunity.Web` and `UmbracoCommunity.Web.UI`. Covers the request flow (URL → render controller → view model builder → view), the three controller flavours, the builder pattern and DI registration, bootstrapping through composers, output caching policies, and the self-contained `Features/Sessionize/` module pattern.
- **[Multi-tenancy primer](multi-tenancy.md)** — one Umbraco instance, several sites. The single invariant (scope every content lookup to the current request's tenant), the multi-root content tree, the `GetSiteSettings()` / `AncestorOrSelf<T>()` helpers, domain-binding for routing-level lookups with no current page, and the intentional cross-tenant exceptions. Threads together the multi-tenant tutorial suite.
- **[Backoffice extensions primer](backoffice.md)** — the three backoffice client codebases (`UmbracoCommunity.Extensions`, `UmbracoCommunity.BlockRestrictions`, `Umbraco.Community.NotFoundTracker`). Covers the App_Plugins `umbraco-package.json` manifest, the bundle pattern, how dashboards / property editors / workspace views register and scope themselves via conditions, the `@umbraco-cms/backoffice` design system and `UMB_AUTH_CONTEXT`, calling secured C# APIs with the user's bearer token, and why each client is its own library-mode Vite project.
- **[Caching primer](caching.md)** — the dozen scattered caches mapped to the four questions they answer (deploy-time artefacts, API responses, expensive cross-request compute, slow third-party data), which mechanism each uses (`RuntimeCache`, static `IMemoryCache`, OutputCache policies, two-tier `IMemoryCache`, stale-fallback feeds), and what invalidates each. Includes a lookup table and "which to reach for" guidance.
- **[Third-party integrations primer](integrations.md)** — the external dependencies: three server-side data feeds (Sessionize, Calendar, Community Blogs) that all follow one resilient pattern (options + named `HttpClient` + cache-with-stale-fallback + composer), plus the avatar/build-time bits. Notes what *isn't* here (Matomo, Intercom, Maps, release tracking) so readers don't go hunting.

### Planned

The backlog in [`IDEAS.md`](./IDEAS.md) lists primers that haven't been written yet. Each idea there also has a placeholder file in this folder with a status callout and a "what this will cover" sketch — useful if you're picking one up to write, or just want to scan what's coming without reading the backlog index.

## Adding a new primer

A new primer earns its keep when an area of the codebase has:

- More than one or two surface-level concepts a new contributor needs to hold in their head, **and**
- A scattering of how-to / tutorial / reference docs that benefit from being threaded together.

If the answer fits in a paragraph in CLAUDE.md, it doesn't need a primer. If you find yourself writing the same "let me explain how X works in this project" thread three times, it does.

See [`IDEAS.md`](./IDEAS.md) for candidate primers worth writing. Each one already has a stub file in this folder — expand the stub in place rather than creating a new file, then move the entry out of the backlog and into the **What's here** list above in the same commit.

You don't need to maintain a contributors list by hand. Each rendered doc shows a **Contributors** section generated from git history (`docs/contributors.generated.json`, produced by `npm run generate:doc-contributors` and refreshed in CI). Open a PR and you'll be credited automatically — with your GitHub avatar where your commit email is linked to your account.

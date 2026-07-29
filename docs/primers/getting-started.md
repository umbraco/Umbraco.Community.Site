---
tags: [primer, getting-started, setup, contributing]
---

# Getting started primer

Before any of the other primers are useful, you need a running site. This one is the on-ramp: what to install, the one command that gets you from a fresh clone to a working local site, and where the guardrails are before you start changing things. It doesn't replace [`Readme.md`](../../Readme.md) or [`BUILD.md`](../../BUILD.md) — those are the command references, kept up to date as the build script changes — this primer is the orientation layer around them, with links out to the primer that covers each area in depth.

> Just want the one command? Skip to [The one-command quick start](#the-one-command-quick-start).

## What you need installed

- **.NET 10 SDK** — the whole backend targets `net10.0`.
- **Node.js 20.9+ and npm 10.1+** — pinned in [`package.json`'s `engines` field](../../src/UmbracoCommunity.StaticAssets/package.json) for the frontend project; the build script checks and installs `node_modules` for you where they're missing.
- **Git.**

One thing you *don't* need for day-to-day work, despite `.config/dotnet-tools.json` listing `dotnet-ef` as a local tool: the three backoffice packages with their own database (Block Restrictions, Blog Announcements, NotFoundTracker) and the member-profiles feature each apply their EF Core migrations automatically at startup via a hosted service — nobody runs `dotnet ef database update` by hand. You only need `dotnet tool restore` (which installs `dotnet-ef` from that manifest) the day you're *authoring* a new migration for one of them.

## The one-command quick start

```bash
node build.mjs
```

On a fresh clone this detects there's no database, offers to download the latest community content snapshot, and runs first-time setup — say yes, and a few minutes later you have a fully working site with real content, running against SQLite, with Umbraco's unattended-install credentials ready to sign in with (see these in appsettings). Subsequent runs detect the existing database and skip straight to starting the dev servers.

`node build.mjs seed` queues a fresh content snapshot for the next boot, keeping your existing DB schema; `node build.mjs reset` renames your local database aside (timestamped, not deleted) and re-runs first-time setup as if from a clean clone. Both are also available from the same interactive menu you get by running `node build.mjs` with no arguments.

See [`Readme.md`](../../Readme.md) for the full quick-start walkthrough and [`BUILD.md`](../../BUILD.md) for every mode the script supports (`dev`, `dev:dotnet`, `local`, `local:dotnet`) and the launch profiles behind them.

## Running the two processes yourself

The build script is the `dev:dotnet` mode wrapped in a nicer first-run experience; if you'd rather run things separately — most commonly so your IDE, not a terminal, owns the `dotnet run` process for breakpoints — it's two processes in two terminals:

```bash
cd src/UmbracoCommunity.Web.UI && dotnet run
cd src/UmbracoCommunity.StaticAssets && npm run dev   # npm ci first if node_modules is missing
```

The Vite dev server runs on `:5123` and gives you hot module replacement for frontend assets; the backend serves the actual site and proxies to it. Razor views, unlike TypeScript/CSS, don't hot-reload — see the [backend primer](backend.md#day-to-day) for what does and doesn't need a restart.

## Local secrets

`appsettings.Local.json` under `src/UmbracoCommunity.Web.UI/` is gitignored and overlays `appsettings.json` automatically when present — that's where API keys and connection strings you don't want committed belong. The GitHub OAuth client id/secret (see the [GitHub OAuth tutorial](../tutorials/foundations/github-oauth-member-authentication.md)) is the one you'll hit first if you're touching member sign-in; without it, that composer just no-ops and sign-in is unavailable, which is fine for most other work.

## The shape of the solution

One `.sln` holds ten projects: the main web app (`UmbracoCommunity.Web.UI` + `UmbracoCommunity.Web`), a Vite-built frontend (`UmbracoCommunity.StaticAssets`), four backoffice extension packages each with their *own* Vite client project (`UmbracoCommunity.Extensions`, `UmbracoCommunity.BlockRestrictions`, `Umbraco.Community.NotFoundTracker`, `UmbracoCommunity.BlogAnnouncements`), and three test projects. `node build.mjs`'s `dev`/`local` modes build all the backoffice clients for you; you won't usually need to build them individually. The [backend primer](backend.md) has the full folder-by-folder map, and the [backoffice extensions primer](backoffice.md) covers why each client is its own library-mode Vite project rather than one shared build.

## One invariant to know before you touch content code

This site runs **several tenants from one Umbraco instance** — several distinct sites, each with its own root content node, sharing one deployment. The one rule that matters: every content lookup scopes to the current request's tenant, never a hardcoded path or "the first root". Read the [multi-tenancy primer](multi-tenancy.md) before writing your first view-model builder or content lookup — it's a five-minute read that heads off a whole class of "worked on my tenant, broke the other one" bugs.

## Regenerating Models Builder classes

Umbraco generates a typed C# class per document/element type from whatever's configured in the backoffice — but only when you ask it to. In development, after creating or changing a document type, element type, or composition in the backoffice, trigger a regeneration from the ModelsBuilder dashboard (Settings section) before the property you just added will compile against. The [content-modelling primer](content-modelling.md#models-builder-from-backoffice-to-c) covers the mechanics and the manual-regeneration gotcha in full.

## Running tests

```bash
dotnet test                                          # all three .NET test projects
cd src/UmbracoCommunity.StaticAssets && npm run test # frontend, Vitest
```

## Making a contribution

- **Branch against `develop`**, not `main` — that's this repo's base branch.
- **[`CODE_CONVENTIONS.md`](../../CODE_CONVENTIONS.md)** and **[`ACCESSIBILITY.md`](../../ACCESSIBILITY.md)** are the coding and WCAG standards this codebase holds itself to; skim them before your first PR.
- **[`docs/LESSONS_LEARNED.md`](../LESSONS_LEARNED.md)** covers workflow gotchas — Umbraco upgrades, schema management, urgent fixes — worth a read if something about the deploy or upgrade process feels surprising.

## Where to go next

Once the site is running, the rest of the primer suite is where to go deep:

- **[Frontend primer](frontend.md)** — the Vite-powered public-site build, Lit components, PostCSS.
- **[Backend primer](backend.md)** — controllers, view-model builders, composers, the request pipeline.
- **[Multi-tenancy primer](multi-tenancy.md)** — the one rule from above, in full.
- **[Backoffice extensions primer](backoffice.md)** — the four backoffice client codebases.
- **[Content modelling primer](content-modelling.md)** — document types, compositions, Models Builder.
- **[Caching primer](caching.md)** — the half-dozen caches this codebase reaches for and when.
- **[SEO and structured data primer](seo-and-structured-data.md)** — meta tags, schema markup, sitemaps.
- **[Third-party integrations primer](integrations.md)** — Sessionize, GitHub, and what's *not* actually wired in despite older docs implying otherwise.

And for adding something new rather than reading about what's there: **[`docs/BUILDING_PAGES.md`](../BUILDING_PAGES.md)**, **[`docs/BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md)**, and the [tutorials suite](../tutorials/README.md) for the *why* behind specific patterns.

Welcome aboard!

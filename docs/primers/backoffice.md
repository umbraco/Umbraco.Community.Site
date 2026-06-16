---
tags: [primer, backoffice, extensions, app-plugins, vite]
---

# Backoffice extensions primer

The [frontend primer](frontend.md) covers the public site and deliberately punts on the *backoffice* — the Umbraco admin UI at `/umbraco`. This repo ships three separate backoffice extension codebases, and this primer threads them together: how an extension reaches the backoffice at all (the App_Plugins manifest), how individual pieces of UI register themselves, how those pieces talk to the secured C# APIs behind them, and why each one is its own little Vite project rather than part of the public-site build.

> Just looking for the folder map? Skip to [Where things live](#where-things-live).

## Three packages, same shape

The backoffice code lives in three Razor Class Libraries, each with a `Client/` folder holding its own TypeScript/Vite project:

| Package | What its backoffice UI does | Built output (`App_Plugins/…`) |
| --- | --- | --- |
| [`UmbracoCommunity.Extensions`](../../src/UmbracoCommunity.Extensions/Client/) | Sessionize dashboard, snapshot-export dashboard, the blog-article entity action, and the event-schedule property editor | `UmbracoCommunityExtensions/` |
| [`UmbracoCommunity.BlockRestrictions`](../../src/UmbracoCommunity.BlockRestrictions/Client/) | The "Blocks" workspace tab on document types, the restricted Block Grid/List property editors, and the file-import dashboard | `UmbracoCommunityBlockRestrictions/` |
| [`Umbraco.Community.NotFoundTracker`](../../src/Umbraco.Community.NotFoundTracker/Client/) | The 404 Tracker dashboard with hit-list and ignore-rule tabs and their modals | `UmbracoCommunityNotFoundTracker/` |

They don't share a build, a `package.json`, or a `node_modules`. Each is self-contained. That's deliberate — see [Why a separate Vite project each](#why-a-separate-vite-project-each) below. The pay-off is that the three follow an almost identical shape, so once you've read one `Client/` folder you can find your way around the others.

## How an extension reaches the backoffice

Umbraco discovers backoffice extensions through a **`umbraco-package.json`** manifest. The flow is short:

```
public/umbraco-package.json   ← Umbraco reads this at boot
  → declares one "bundle" extension pointing at a built .js file
    → that .js exports a `manifests` array
      → which registers every dashboard, property editor, workspace view, …
```

Each package's manifest declares exactly one extension — a **bundle** — that points at the built JS:

```json
{
  "id": "UmbracoCommunity.BlockRestrictions",
  "name": "Block Restrictions",
  "extensions": [
    {
      "type": "bundle",
      "alias": "UmbracoCommunity.BlockRestrictions.Bundle",
      "name": "Block Restrictions Bundle",
      "js": "/App_Plugins/UmbracoCommunityBlockRestrictions/block-restrictions.js"
    }
  ]
}
```

A *bundle* is the recommended pattern: rather than list every extension in JSON, you point at one JS entry point that registers them all in code. That entry point is **`src/bundle.manifests.ts`**. In the larger packages its job is just to collate the per-area manifest arrays into one exported array (smaller ones declare a manifest or two inline instead):

```ts
import { manifests as workspaceViews } from "./workspace-views/manifest.js";
import { manifests as propertyEditors } from "./property-editors/manifest.js";
import { manifests as dashboards } from "./dashboards/manifest.js";

export const manifests: Array<UmbExtensionManifest> = [
  ...workspaceViews,
  ...propertyEditors,
  ...dashboards,
];
```

The `umbraco-package.json` lives in `Client/public/`, so Vite copies it to the output folder verbatim alongside the built JS. (The `.csproj` keeps the whole `Client/` folder out of NuGet packaging but explicitly re-includes `umbraco-package.json` so the solution can see it.)

## Registering a piece of UI

Every dashboard, property editor, and workspace view is one entry in a `manifest.ts` array. The shape is consistent — a `type`, a globally-unique `alias`, the custom element it renders, a lazy `js` import, a `meta` block, and the `conditions` that decide where it shows up. A dashboard manifest is the clearest example:

```ts
{
  type: "dashboard",
  alias: "Umbraco.Community.NotFoundTracker.Dashboard",
  name: "404 Tracker",
  elementName: "not-found-tracker-dashboard",
  js: () => import("./dashboards/not-found-tracker-dashboard.element.js"),
  meta: { label: "404 Tracker", pathname: "not-found-tracker" },
  conditions: [
    { alias: "Umb.Condition.SectionAlias", match: "Umb.Section.Content" },
  ],
}
```

A few things worth internalising from that one object:

- **`js` is a lazy import**, not an eager one. The element module loads only when Umbraco actually needs to render it, which keeps the initial backoffice bundle small.
- **`conditions` are how you scope an extension.** `Umb.Condition.SectionAlias` pins a dashboard to a section (Content, Settings, …); `Umb.Condition.WorkspaceAlias` (used by the Block Restrictions "Blocks" tab) pins a workspace view to `Umb.Workspace.DocumentType` so it doesn't appear on Media or Member types. Conditions can also be custom — Extensions ships an [`is-blog-node` condition](../../src/UmbracoCommunity.Extensions/Client/src/conditions/) that gates the "create blog article" entity action.
- **`alias` must be globally unique** across every extension in the backoffice, hence the reverse-DNS-ish prefixes (`UmbracoCommunity.*`, `Umbraco.Community.NotFoundTracker.*`).

The extension `type`s in use across the three packages: `dashboard`, `workspaceView`, `propertyEditorUi`, `propertyContext`, `propertyAction`, `clipboardCopy`/`clipboardPastePropertyValueTranslator`, `entityAction`, `condition`, and `modal`. The [Umbraco extension-types docs](https://docs.umbraco.com/umbraco-cms/customizing/extending-overview/extension-types) list the full catalogue; these are the ones this repo actually reaches for.

> Wrapping a native editor is rarely one manifest. The restricted Block Grid/List editors need *six* manifests each (the editor UI, plus clipboard + sort-mode contexts, plus copy/paste/sort actions) and a set of clipboard value translators, because the native block editor's own contexts are filtered by `forPropertyEditorUis` and won't load for a custom UI alias. That whole dance is its own story — see the [wrapping-the-native-block-editor tutorial stub](../tutorials/refinements/wrapping-umbraco-native-block-editor.md).

## The design system and element shape

Backoffice UI is built with **`@umbraco-cms/backoffice`** — Umbraco's own design system and component library. It re-exports Lit (so you author standard web components) plus a large library of `<uui-*>` components, contexts, and mixins. Two conventions show up everywhere:

- Components extend `LitElement` wrapped in **`UmbElementMixin`**. The mixin plugs the element into Umbraco's **context API** — a dependency-injection-style system where services are *published* onto a parent element and *consumed* by descendants, rather than imported as singletons.
- You obtain a service by **consuming its context token**: `consumeContext(TOKEN, callback)` invokes the callback (now, or later, whenever the service becomes available up the tree) with the resolved service. The token that matters most to us is `UMB_AUTH_CONTEXT`, whose service can hand back the current backoffice bearer token.

```ts
import { LitElement, html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";

@customElement("block-restrictions-workspace-view")
export class BlockRestrictionsWorkspaceView extends UmbElementMixin(LitElement) {
  constructor() {
    super();
    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;                          // guard: don't clobber on disconnect
      const config = authContext.getOpenApiConfiguration();
      setAuthConfig({ token: config?.token, baseUrl: config?.base });  // hand the token getter to the API client
    });
  }
}
```

`config.token` is itself a *function* that returns a fresh token on each call — the element stashes it via `setAuthConfig` so the API client (below) can call it per request. The `if (!authContext) return` guard matters: the callback also fires with `undefined` when the element disconnects, and skipping that case avoids wiping the stored token mid-session.

Note the Lit import path: most backoffice elements import from `@umbraco-cms/backoffice/external/lit` rather than the bare `lit` package the public site uses, because the backoffice provides Lit at runtime and elements should share that one instance instead of bundling their own. (A few elements in this repo still import bare `lit`; prefer the `/external/lit` path in new code.)

## Talking to the C# APIs

Most of these extensions are a thin UI over a secured backoffice Management API controller (e.g. `BlockRestrictionApiController`, routed under `/umbraco/.../api/v1` and guarded by `[Authorize(Policy = …)]`). The client side needs to attach the user's backoffice bearer token to every request. The pattern: the element consumes `UMB_AUTH_CONTEXT` and stashes its token getter (the `setAuthConfig` call shown above), and a small fetch wrapper then resolves a fresh token from that getter and adds the `Authorization` header on every call:

```ts
async function fetchWithAuth(url: string, options: RequestInit = {}) {
  const token = await resolveToken();           // calls the getter stashed via setAuthConfig
  const headers = { "Content-Type": "application/json", ...options.headers };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  return fetch(`${baseUrl}${url}`, { ...options, headers, credentials: "same-origin" });
}
```

There are two flavours of client in the repo, and the difference is worth knowing before you add an endpoint:

- **Hand-written** ([BlockRestrictions `api/client.ts`](../../src/UmbracoCommunity.BlockRestrictions/Client/src/api/client.ts), [NotFoundTracker `api/not-found-tracker-api.ts`](../../src/Umbraco.Community.NotFoundTracker/Client/src/api/not-found-tracker-api.ts)) — typed functions and DTO interfaces written by hand. Simple, no codegen step, but the types can drift from the C# DTOs if you forget to update both.
- **Generated** ([Extensions `api/*.gen.ts`](../../src/UmbracoCommunity.Extensions/Client/src/api/)) — produced by `@hey-api/openapi-ts` from the controller's Swagger document via `npm run generate-client`. The C# side is the source of truth; you re-run the generator after changing a controller.

→ The secured-API side — routing under `/umbraco`, the `AuthorizationPolicies` you decorate with, Swagger registration, and the token-bearing fetch wrapper — is the subject of a planned [backoffice Management API tutorial](../tutorials/foundations/backoffice-management-api-with-auth-policies.md). Community content on the new backoffice almost always stops at the property-editor UI; the C# it calls is consistently under-documented, so that one's worth writing.

## Why a separate Vite project each

Each `Client/` builds in Vite's **library mode** rather than the public site's app/manifest mode:

```ts
// vite.config.ts — BlockRestrictions (the others differ only in names + externals)
export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: "block-restrictions",
    },
    outDir: "../wwwroot/App_Plugins/UmbracoCommunityBlockRestrictions",
    emptyOutDir: true,
    rollupOptions: { external: [/^@umbraco/, /^lit/] },
  },
});
```

The two lines that matter:

- **`outDir`** writes straight into the package's own `wwwroot/App_Plugins/<Name>/`. No copy step, no manifest hand-off — the bundle lands exactly where Umbraco looks for it.
- **`rollupOptions.external`** tells Rollup *not* to bundle those packages — emit bare `import` statements for them and assume the host provides them at runtime. The backoffice ships its own copy of `@umbraco/*` (and Lit) and exposes them through its **import map** (the browser feature that resolves a bare specifier like `lit` to a real URL). Bundling them would ship a second copy and, worse, a second Lit instance that doesn't share the backoffice's reactive context. The exact list varies by package — BlockRestrictions externalises `@umbraco` and `lit`; Extensions externalises only `@umbraco` (its elements import Lit through `@umbraco-cms/backoffice`, so it's already covered); NotFoundTracker externalises `@umbraco-cms/backoffice`.

Why not fold this into the public-site `StaticAssets` build? Because the two targets barely overlap: the backoffice has different module-loading conventions, host-provided `@umbraco/*`, no manifest/HMR story, and a different externalisation list. The repo *did* once build the backoffice from `StaticAssets` behind a `BUILD_TARGET` switch and the gymnastics weren't worth it; each client is small enough to own its toolchain cleanly.

## Building

The backoffice clients are **not** built by `dotnet build` — there's no MSBuild target invoking npm, so you build them by hand when you've changed backoffice TypeScript:

```bash
cd src/UmbracoCommunity.Extensions/Client        # or BlockRestrictions / NotFoundTracker
npm ci          # first time only
npm run build   # tsc type-check, then vite build → ../wwwroot/App_Plugins/<Name>/
```

`npm run watch` (where present) does `tsc && vite build --watch` for an edit-rebuild loop. There's no HMR for the backoffice — after a rebuild, hard-refresh the `/umbraco` tab to pick up the new bundle. The built output under each package's `wwwroot/App_Plugins/<Name>/` is **gitignored**, not committed — so anything that packages or deploys a backoffice package has to run the npm build first to produce the bundle the RCL then ships as a static web asset.

## Where things live

A `Client/` folder follows this layout (Block Restrictions shown — the others are subsets of the same):

| Path | What's in it |
| --- | --- |
| `public/umbraco-package.json` | The manifest Umbraco reads; declares the bundle. Copied to output verbatim |
| `src/bundle.manifests.ts` | Bundle entry point — collates every area's `manifests` array |
| `src/<area>/manifest.ts` | Per-area registrations (`dashboards/`, `property-editors/`, `workspace-views/`, `entity-actions/`, `conditions/`) |
| `src/<area>/*.element.ts` | The Lit web components those manifests point at |
| `src/api/` | The typed API client — hand-written (`client.ts`) or generated (`*.gen.ts`) |
| `src/**/*.test.ts` | Vitest tests, colocated (BlockRestrictions tests its clipboard translators here) |
| `vite.config.ts` / `tsconfig.json` | Library-mode build config; TS targets `@umbraco-cms/backoffice/extension-types` |
| `../wwwroot/App_Plugins/<Name>/` | Build output — the committed bundle Umbraco serves |

## Related docs

- **[`src/UmbracoCommunity.BlockRestrictions/README.md`](../../src/UmbracoCommunity.BlockRestrictions/README.md)** — the deepest worked example of a backoffice package end to end (property editors, workspace view, dashboard, EF Core migrations, dual DB/JSON persistence).
- **[Backend primer](backend.md)** — the C# side these clients call into: controllers, composers, services.
- **[Frontend primer](frontend.md)** — the public-site Vite project, for contrast with the backoffice builds.
- **[`docs/BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md)** — adding a content block (the *content* side; this primer is the *editor-UI* side).

For *why* specific backoffice pieces are shaped the way they are — wrapping the native block editor, shipping EF Core migrations from a package, the secured Management API — see the [tutorials suite](../tutorials/README.md). Several of those are still stubs in [`IDEAS.md`](../tutorials/IDEAS.md); if you've just been spelunking in one of these `Client/` folders, you're in a good position to write one.

Hopefully that's enough to find your way around the admin side — happy extending!

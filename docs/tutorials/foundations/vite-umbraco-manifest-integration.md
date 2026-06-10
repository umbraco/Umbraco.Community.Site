---
tags: [vite, manifest, razor, tag-helper, hmr]
---

# Wiring Vite's manifest into Umbraco's Razor pipeline

Vite bundles your frontend and Umbraco renders your views, and sitting between the two is a single `<script>` tag that needs to behave completely differently depending on where it's running. Locally, you want the browser talking to the Vite dev server so you get hot module replacement (HMR — your edits to JS and CSS show up in the browser without a full page reload). In production, you want it loading a content-hashed file whose name changes with every build. Same tag, two different usecases. This tutorial walks through the pair of TagHelpers the Umbraco Community site uses to bridge them — `<script vite-src>` and `<link vite-href>` — so that you can write one stable line of Razor and lets the C# work out, per environment, whether to point at `localhost:5123` for HMR or at the hashed asset named in Vite's `manifest.json`.

This is a *foundation* piece. Anything else frontend-flavoured in the codebase ultimately renders through these two TagHelpers — the one exception being the Umbraco backoffice extensions, which are built by [separate Vite projects](../../primers/frontend.md#other-frontend-codebases) rather than this one. The pattern transfers to any ASP.NET Core project — there's nothing Umbraco-specific about the core idea — but it's the bit almost every "use Vite with .NET" guide online either skips or fumbles, so it earns a write-up.

If you've not written a TagHelper before: it's a small Razor extension that looks like an HTML element in your view but is expanded server-side, in C#, before the page is sent to the browser (Microsoft's [Introduction to Tag Helpers](https://learn.microsoft.com/aspnet/core/mvc/views/tag-helpers/intro) is the canonical primer if you want the fuller picture). We lean on that server-side step to make the dev/prod decision.

## Credit where it's due

The code shown here is **adapted from the Vite TagHelpers in [`Umbraco.Demo.Cloud`](https://github.com/umbraco/Umbraco.Demo.Cloud/tree/master/Umbraco.Demo.Cloud.Core/Vite)** — each file in this repo's `Vite/` folder carries a "Hat tip" comment pointing back at its origin. The two-mode `ProcessAsync` shape, the manifest model, and the `@vite/client` bootstrap are theirs. What's changed here is the entry-name convention (the `_*.ts` entrypoint scheme and extensionless lookups), the `IFileProvider.Watch`-based cache invalidation, and integration with this site's nonce/CSP setup. If you're starting fresh, read their version alongside this one.

## Why you might want this

Why not just point the `<script>` straight at the file and have done with it? Because Vite's output is content-hashed. A production build doesn't emit `index.js`; it emits `_index-Bf3kq9x2.js`, and that hash changes whenever the source changes. That's exactly what you want for cache-busting — you can serve the file with a one-year `immutable` cache header and never worry about a stale bundle landing in someone's browser — but it does mean **you cannot hardcode the filename in your view.** You don't know the name until the build runs, and it'll be a different name next build.

Vite solves the "what's the real filename?" problem with a `manifest.json` it writes at build time: a map from the source entry path you *do* know (`src/entrypoints/_index.ts`) to the hashed output file you *don't* (`assets/_index-Bf3kq9x2.js`), plus the CSS files and chunks that entry pulls in (the shape of that file, and this whole server-reads-the-manifest dance, is documented in Vite's [Backend Integration guide](https://vite.dev/guide/backend-integration.html)). So in production your server needs to read that manifest and emit the right `<script src>`.

But in *development* you want none of that. You want the Vite dev server running with HMR, so editing a Lit component or a stylesheet updates the page without a reload. In dev there's no manifest and no hashed file — there's a long-running server on a port, and your `<script>` should point straight at it.

So the same view has to render two completely different `<script>`/`<link>` outputs depending on environment. You could litter `@if (env.IsDevelopment())` blocks through every layout, but that's the thing this pattern exists to avoid.

## What we're building

The Razor view incorporates two TagHelpers, written once, that resolve correctly whatever the environment:

```cshtml
<link rel="stylesheet" vite-href="index" asp-add-nonce="true" />
@* … page body … *@
<script vite-src="index" vite-client="true" asp-add-nonce="true"></script>
```

(Ignore `asp-add-nonce` for now — that's a separate Content-Security-Policy concern, not part of the Vite wiring. It stamps a security token on the tag so a strict CSP will allow it.)

In **development** that code resolves to a *module script* (`type="module"` — an ES module the browser loads natively) pointing at the Vite dev server, which also injects the CSS, so the `<link>` renders nothing — plus a one-time `@vite/client` bootstrap that wires up HMR:

```html
<script type="module" src="https://localhost:5123/@vite/client"></script>
<script type="module" src="https://localhost:5123/src/entrypoints/_index.ts"></script>
```

In **production** the same code reads the `manifest.json` data and emits the hashed asset and its stylesheet(s):

```html
<link rel="stylesheet" href="/assets/_index-Bf3kq9x2.css" />
<script type="module" src="/assets/_index-Bf3kq9x2.js"></script>
```

The code never has to make that distinction. It references a logical entry — `index`, `blog`, or `@currentPage.ContentTypeAlias` for a per-page bundle — and the C# resolves it.

The moving parts:

- **`ViteTagHelperBase`** — the shared base: holds config (dev-server URL, manifest path), the entry-name normaliser, and the cached manifest reader.
- **`ViteScriptTagHelper`** (`<script vite-src>`) and **`ViteLinkTagHelper`** (`<link vite-href>`) — the two concrete helpers, each with a two-branch `ProcessAsync`: dev branch, prod branch.
- **`ViteManifest` / `ViteManifestEntry`** — the typed shape of Vite's `manifest.json`.
- **A build step** (`vite.config.ts` + `copy-for-cloud.js`) that produces the manifest and lands the assets where the helpers expect them.

## Walkthrough

The code lives in [`src/UmbracoCommunity.Web/Vite/`](../../../src/UmbracoCommunity.Web/Vite/). We'll go base → script → link → manifest model → build, because each step leans on the one before.

### Step 1 — The entry-name convention

Before any TagHelper runs, there's a naming convention that makes the whole thing ergonomic. Vite entrypoints in this repo live in `src/entrypoints/` and are prefixed with an underscore — `_index.ts`, `_blog.ts`, `_digital-signage.ts`. The Vite config globs them as build inputs:

```ts
// vite.config.ts
rollupOptions: {
    input: glob.sync("src/entrypoints/_*.ts").map((file) => file),
    // …
}
```

Vite keys each manifest entry by its source path relative to the project root — so `_index.ts` becomes the manifest key `src/entrypoints/_index.ts`. Rather than require that whole path in the markup, `TryGetEntryName` in [`ViteTagHelperBase`](../../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteTagHelperBase.cs) reconstructs it from the short name:

```csharp
protected bool TryGetEntryName(string? contentPath, IUrlHelper urlHelper, [NotNullWhen(true)] out string? entryName)
{
    entryName = default;
    if (contentPath is null) return false;

    string path = urlHelper.Content(contentPath).ToLowerInvariant();
    if (string.IsNullOrEmpty(path)) return false;

    if (!path.StartsWith("/src/entrypoints/_"))
    {
        if (path.StartsWith('_'))
            path = $"/src/entrypoints/{path}";
        else
            path = $"/src/entrypoints/_{path}";
    }

    path = path.TrimStart('/');

    // The src extension is always .ts; the manifest entry covers both the .js and .css
    // outputs, so callers never need to name an extension — they request by bundle name.
    if (Path.ChangeExtension(path, null) is string extensionlessPath)
    {
        entryName = $"{extensionlessPath}.ts";
        return true;
    }

    return false;
}
```

So `vite-src="index"` → `src/entrypoints/_index.ts`, which is exactly the manifest key. The view names a *bundle*, not a file, and the same name works for both the script and the stylesheet because one manifest entry carries both. This is also why `vite-href="@currentPage.ContentTypeAlias"` works as a per-page convention: every page type that has an `_<alias>.ts` entrypoint automatically gets its bundle, and the helper silently suppresses output for the ones that don't (more on that below).

### Step 2 — Config and the dev/prod switch

The base class reads two settings, with sensible defaults, in its constructor:

```csharp
public ViteTagHelperBase(IUrlHelperFactory urlHelperFactory, IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
{
    UrlHelperFactory = urlHelperFactory;
    _webHostEnvironment = webHostEnvironment;

    ViteManifestPath = configuration["ViteManifestPath"] ?? "/assets/manifest.json";
    ViteDevServerUrl = configuration["ViteDevServerUrl"] ?? "https://localhost:5123";
}

protected bool IsDevelopmentEnvironment() => _webHostEnvironment.IsDevelopment();
```

Both have defaults, so neither needs to appear in `appsettings.json` for the standard setup — they're override points, not required config. `IsDevelopmentEnvironment()` is the single switch every `ProcessAsync` branches on; it's just `IWebHostEnvironment.IsDevelopment()`, driven by `ASPNETCORE_ENVIRONMENT`.

### Step 3 — The script TagHelper

[`ViteScriptTagHelper`](../../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteScriptTagHelper.cs) targets `<script vite-src>` and carries the dev/prod fork:

```csharp
[HtmlTargetElement("script", Attributes = ViteSrcAttributeName)]
public class ViteScriptTagHelper : ViteTagHelperBase
{
    [HtmlAttributeName("vite-src")] public string? Src { get; set; }
    [HtmlAttributeName("vite-client")] public bool? Client { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        IUrlHelper urlHelper = UrlHelperFactory.GetUrlHelper(ViewContext);

        if (string.IsNullOrEmpty(Src) || !TryGetEntryName(Src, urlHelper, out string? entryName))
        {
            output.SuppressOutput();
            return;
        }

        if (IsDevelopmentEnvironment())
        {
            output.Attributes.SetAttribute("src", ViteDevServerUrl + "/" + entryName);
            output.Attributes.SetAttribute("type", "module");

            // Render the HMR client only when asked, so it appears exactly once per page.
            if (Client is true)
            {
                TagBuilder scriptTag = new("script");
                scriptTag.Attributes["type"] = "module";
                scriptTag.Attributes["src"] = ViteDevServerUrl + "/@vite/client";
                output.PreElement.AppendHtml(scriptTag);
            }

            return;
        }

        if (await GetViteManifestAsync(urlHelper) is ViteManifest viteManifest &&
            viteManifest.TryGetValue(entryName, out ViteManifestEntry? viteManifestEntry))
        {
            output.Attributes.SetAttribute("src", EntryNameWithBase(viteManifestEntry.File));
            output.Attributes.SetAttribute("type", "module");
            return;
        }

        output.SuppressOutput();
    }
}
```

Two of the calls here live in the base class: `GetViteManifestAsync` reads the cached manifest (Step 5), and `EntryNameWithBase` prefixes the asset path (the third point below). With those parked, three things in this method are worth pausing on:

**The `vite-client` flag and "exactly once per page".** Vite's HMR needs its own client script (`@vite/client`) loaded once — it's what opens the websocket back to the dev server and applies updates. But a page often pulls in *several* bundles (`index` plus a per-page bundle, say), and you only want one HMR client. Rather than track "have I emitted it yet?" across helper instances, the flag is explicit: exactly one `<script vite-src>` in the layout sets `vite-client="true"`, and only that one emits the bootstrap. In [`Layout.cshtml`](../../../src/UmbracoCommunity.Web.UI/Views/Layout.cshtml) it's the shared `index` bundle:

```cshtml
<script vite-src="index" vite-client="true" asp-add-nonce="true"></script>
<script vite-src="@currentPage.ContentTypeAlias" asp-add-nonce="true"></script>
```

The per-page bundle deliberately omits the flag. In production `vite-client` does nothing — the branch that reads it never runs — so it's a dev-only concern that's harmless to leave in the markup.

**`SuppressOutput()` is the graceful-degradation path.** If the entry name can't be resolved, or the manifest has no matching entry, the helper removes itself from the output entirely rather than emitting a broken `<script src="">`. That's what makes `vite-src="@currentPage.ContentTypeAlias"` safe to put in the shared layout: a page type with no matching entrypoint simply renders no script tag instead of a 404'ing one.

**`EntryNameWithBase` is where the `/assets/` prefix comes from.** It's a one-liner — `$"/assets/{entryName}"` — and it's the contract with the build step: the production assets are served under `/assets/`. Step 6 is where they get there.

### Step 4 — The link TagHelper

[`ViteLinkTagHelper`](../../../src/UmbracoCommunity.Web/Vite/TagHelpers/ViteLinkTagHelper.cs) targets `<link vite-href>` and is the asymmetric twin of the script helper — its dev branch does *nothing*:

```csharp
public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
{
    if (IsDevelopmentEnvironment() || string.IsNullOrEmpty(Href))
    {
        // In dev, the JS chunk injects the stylesheet itself — emitting a <link> would double-load it.
        output.SuppressOutput();
        return;
    }

    if (UrlHelperFactory.GetUrlHelper(ViewContext) is IUrlHelper urlHelper &&
        TryGetEntryName(Href, urlHelper, out string? entryName) &&
        await GetViteManifestAsync(urlHelper) is ViteManifest viteManifest &&
        viteManifest.TryGetValue(entryName, out ViteManifestEntry? viteManifestEntry) &&
        viteManifestEntry.Css?.Length > 0)
    {
        output.Attributes.SetAttribute("href", EntryNameWithBase(viteManifestEntry.Css[0]));

        // A single entry can pull in multiple stylesheets; clone the <link> for each extra one.
        foreach (string css in viteManifestEntry.Css[1..])
        {
            TagBuilder linkTag = new("link");
            foreach (TagHelperAttribute attribute in output.Attributes)
                linkTag.Attributes[attribute.Name] = attribute.Value?.ToString();

            linkTag.Attributes["href"] = EntryNameWithBase(css);
            output.PostElement.AppendHtml(linkTag);
        }

        return;
    }

    output.SuppressOutput();
}
```

The dev-mode no-op is the subtle part, and it's the source of a lot of confusion when people first wire Vite into a server-rendered app. In development, Vite serves CSS *through JavaScript* — the dev server's JS imports the stylesheet and injects a `<style>` tag into the page at runtime, so HMR can hot-swap CSS without a reload. If you *also* emitted a `<link rel="stylesheet">` you'd load the styles twice (and the static `<link>` wouldn't hot-reload). So in dev the link helper suppresses itself entirely and lets the JS own the CSS. In production there's no dev server, the CSS is a real hashed file, and the helper emits the `<link>` (or several, since one entry can split into multiple stylesheets — the `Css[1..]` loop, which is "every stylesheet after the first", clones the tag for each extra one, preserving attributes like the nonce).

### Step 5 — Reading and caching the manifest

Both prod branches call `GetViteManifestAsync`, in the base class. This is where the "survives a deploy without an app restart" claim is earned:

```csharp
private static IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());

protected async Task<ViteManifest?> GetViteManifestAsync(IUrlHelper urlHelper)
{
    string? viteManifestPath = urlHelper.Content(ViteManifestPath);
    if (viteManifestPath is null) return default;

    return await Cache.GetOrCreateAsync(viteManifestPath, async cacheEntry =>
    {
        // Expire the cache entry when the manifest is added, modified, or deleted on disk.
        cacheEntry.AddExpirationToken(_webHostEnvironment.WebRootFileProvider.Watch(viteManifestPath));

        IFileInfo viteManifestFileInfo = _webHostEnvironment.WebRootFileProvider.GetFileInfo(viteManifestPath);
        if (viteManifestFileInfo.Exists)
        {
            using Stream stream = viteManifestFileInfo.CreateReadStream();
            return await JsonSerializer.DeserializeAsync<ViteManifest>(stream, _serializerOptions);
        }

        return null;
    });
}
```

Reading and deserialising JSON on every page render would be wasteful — the manifest only changes at deploy time. So it's cached in a static [`IMemoryCache`](https://learn.microsoft.com/aspnet/core/performance/caching/memory). The clever bit is the invalidation: rather than a time-based expiry (which would mean either stale assets for a window after deploy, or needless re-reads), it registers a **[change token](https://learn.microsoft.com/aspnet/core/fundamentals/change-tokens) from `IFileProvider.Watch`**. The file provider watches `manifest.json` on disk; the moment a deploy overwrites it, the token fires, the cache entry is evicted, and the next request re-reads the fresh manifest. No TTL to tune, no app restart, no stale-bundle window. The serializer uses `CamelCase` naming to match Vite's manifest keys (`file`, `css`, `imports`, …).

`ViteManifest` is just a typed dictionary, and `ViteManifestEntry` mirrors Vite's documented manifest shape:

```csharp
public sealed class ViteManifest : Dictionary<string, ViteManifestEntry> { }

public sealed class ViteManifestEntry
{
    public string File { get; set; } = null!;     // the hashed JS output
    public string? Src { get; set; }
    public bool IsEntry { get; set; }
    public string[]? Imports { get; set; }
    public string[]? DynamicImports { get; set; }
    public string[]? Css { get; set; }            // hashed stylesheet(s) for this entry
    public string[]? Assets { get; set; }
}
```

The script helper reads `.File`; the link helper reads `.Css`. The rest of the fields aren't used by these two helpers but are deserialised so the model is faithful to Vite's format (handy if you later want to emit `<link rel="modulepreload">` tags for `Imports`).

### Step 6 — The build: where the manifest and assets come from

The TagHelpers assume two things at runtime: a `manifest.json` under `/assets/`, and the hashed files it names sitting alongside it. The Vite config produces them, and a copy step puts them where ASP.NET serves static files.

`vite.config.ts` turns on the manifest and writes to `dist/`:

```ts
build: {
    manifest: true,        // emit dist/.vite/manifest.json
    outDir: "./dist",
    emptyOutDir: true,
    rollupOptions: {
        input: glob.sync("src/entrypoints/_*.ts").map((file) => file),
        output: {
            entryFileNames: "[name]-[hash].js",
            assetFileNames: "[name]-[hash][extname]",
            // …
        },
    },
}
```

`npm run build` produces `dist/` plus `dist/.vite/manifest.json`. Then [`devops/copy-for-cloud.js`](../../../src/UmbracoCommunity.StaticAssets/devops/copy-for-cloud.js) (run by `npm run build:for:cloud`) copies the lot into the web project's `wwwroot/assets/` and lifts the manifest up to the top of that folder:

```js
const srcDir = './dist';
const outputDir = '../UmbracoCommunity.Web.UI/wwwroot/assets';
const manifestSrc = './dist/.vite/manifest.json';

rmSync(outputDir, { recursive: true, force: true });
cpSync(srcDir, outputDir, { recursive: true });
copyFileSync(manifestSrc, `${outputDir}/manifest.json`);   // → /assets/manifest.json
rmSync(`${outputDir}/.vite`, { recursive: true, force: true });
```

Because `wwwroot/assets/` is under `wwwroot`, ASP.NET's [static-file middleware](https://learn.microsoft.com/aspnet/core/fundamentals/static-files) serves it at `/assets/` with no extra configuration. That closes the loop: `EntryNameWithBase` prefixes `/assets/`, the manifest defaults to `/assets/manifest.json`, and the copy step is what makes both paths real. The `[hash]` in the filenames means you can cache those files aggressively — the name changes whenever the content does.

### Step 7 — Registering the helpers

TagHelpers have to be registered in `_ViewImports.cshtml` before a view can use them. They're picked up by the assembly-wide registration that's already there for the rest of the site's helpers:

```cshtml
@addTagHelper *, UmbracoCommunity.Web
```

That's it — `vite-src` and `vite-href` are now available in every view under that `_ViewImports`.

## Alternatives we considered

- **Vite's [official backend-integration recipe](https://vite.dev/guide/backend-integration.html) (read the manifest in app code, hand it to the view).** This is essentially what we've built, just packaged as TagHelpers instead of a view-model field. The TagHelper packaging is what keeps the view readable — `vite-src="index"` instead of threading a manifest object through every page's model. For a non-Razor stack you'd do the equivalent in whatever your view layer's extension mechanism is.
- **A Node-based SSR/middleware integration (`vite-plugin-ssr`, the `vite` middleware mode).** Overkill here. The site is server-rendered by ASP.NET, not Node; we want Vite for *asset bundling and dev HMR*, not to hand it the rendering pipeline. Running a Node server in front of (or beside) Kestrel just to serve assets adds an ops surface we don't need.
- **`WebpackTagHelper` / `webpack-manifest` equivalents.** Same shape, different bundler. We picked Vite for the dev-server speed and native ES-module support; the manifest-reading half of this pattern would be nearly identical with any bundler that emits a manifest.
- **Hardcoding asset paths and disabling content hashing.** Removes the manifest entirely — but then you lose long-lived immutable caching and have to bust caches some other way (query strings, which proxies handle inconsistently). The manifest is the price of good caching, and it's a small price once these helpers exist.
- **A single combined TagHelper for both script and link.** Tempting, but `<script>` and `<link>` are different elements with different target attributes and, crucially, *opposite* dev-mode behaviour (script points at the dev server; link suppresses itself). Two helpers sharing a base class expresses that asymmetry more honestly than one helper with a mode flag.

## Trade-offs and known limits

- **"I forgot to start `npm run dev`" is the classic dev failure mode.** In development the helpers point `<script src>` at `https://localhost:5123/...` and suppress the stylesheets (the JS injects them). If the Vite dev server isn't running, the page renders its HTML but loads no scripts and no styles — you get an unstyled, inert page, and the browser console shows `net::ERR_CONNECTION_REFUSED` against `:5123`. Recognising it is the fix: start the dev server. It catches everyone once. (See the [development setup notes](../../../CLAUDE.md) — both `dotnet run` and `npm run dev` need to be running.)
- **The dev-server URL and port are convention, not coordination.** `vite.config.ts` runs Vite on `5123` (`"dev": "vite --port 5123"`) and the TagHelper defaults to `https://localhost:5123`. Change one and you must change the other (via `ViteDevServerUrl` config). There's no shared source of truth tying the two together.
- **HTTPS in dev relies on `vite-plugin-mkcert`.** The default dev-server URL is `https://`, so Vite has to serve over TLS. The config uses `mkcert()` to generate a locally-trusted cert; without it (or with an untrusted cert) the browser blocks the module scripts as mixed/insecure content and you're back to the unstyled-page symptom.
- **The manifest cache is process-local.** It's a static `IMemoryCache`, so each app instance reads and caches its own copy. That's fine — the `IFileProvider.Watch` token invalidates each instance independently when its own `wwwroot/assets/manifest.json` changes on deploy. On a platform where the manifest is swapped atomically this is exactly right; if a deploy left the manifest and the files it names briefly out of sync, a request landing in that window could 404 an asset until the file watcher catches up.
- **`vite-client` correctness is a convention, not a guarantee.** Exactly one tag per page must set `vite-client="true"`, and nothing enforces it. Set it on two and you load the HMR client twice in dev (harmless but noisy); set it on none and HMR silently doesn't connect. The convention is "the shared `index` bundle owns it" — worth a comment in any new layout.
- **Entry names are lowercased and `_`-prefixed implicitly.** `TryGetEntryName` lowercases the path and assumes the `src/entrypoints/_*.ts` layout. An entrypoint that doesn't follow that naming, or a name with meaningful casing, won't resolve. The convention is load-bearing; it's the cost of being able to write `vite-src="index"` instead of a full path.

## Where to go next

→ [Frontend primer — other frontend codebases](../../primers/frontend.md#other-frontend-codebases) — the Umbraco backoffice extensions don't share this build; each is its own Vite library-mode project that externalises `@umbraco/*`. The primer's closing section covers how those separate clients are set up.

→ [Building an inline SVG TagHelper for Umbraco](inline-svg-tag-helper.md) — another foundation TagHelper, if you want more practice with the server-side-expansion pattern before writing your own.

Hopefully this saves you the afternoon most people lose to the dev/prod handover the first time they try to put Vite in front of a Razor app.

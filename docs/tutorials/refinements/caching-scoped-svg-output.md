# Caching the scoped SVG output

> **Prerequisites:** This refinement builds directly on [Scoping inline SVG `<style>` to prevent class-name bleed](./scoping-inline-svg-styles.md). The cache layer described below only works because the scope class introduced there is *deterministic per media path*. If you skipped straight here, read that one first — the caching is the easy part once scoping is in place.

Once the SVG TagHelper produces deterministic scoped output, every render of the same media item is byte-identical. That makes the whole pipeline cacheable: we can do the read + sanitise + parse + selector-prefix work *once* and reuse the result for an hour. On cloud-hosted media this is the difference between every page render making N media-storage round-trips and making zero.

## The problem

With scoping in place, the per-render path looks like this for each inline SVG:

1. Resolve the Umbraco media URL.
2. Open the file from the media filesystem and read it into a string.
3. Run two regex sanitisation passes (strip `<script>` blocks, neutralise `javascript:` URLs).
4. Parse the result with HtmlAgilityPack.
5. Walk every `<style>` block and prefix every selector with the scope class.
6. Inject any per-call width/height/alt attributes.
7. Stringify back via `OuterHtml`.

None of those steps are individually expensive, but they all run on every request that renders the SVG. A page like the community site has a logo in the header, a logo in the footer, and a row of social icons — call it ten inline SVGs per page. With media on local SSD the cumulative cost is single-digit milliseconds; on cloud blob storage (S3, Azure Blob, etc.) where each `OpenFile` is a network round-trip of ~50ms, you're now looking at half a second of latency before any HTML reaches the client.

And it's all *waste*. The output for `media/.../logo.svg` is identical every time. We should be computing it once.

## Why the obvious fix doesn't work

Two intermediate caching strategies that don't actually solve the problem:

**"Cache the file contents."** You can wrap step 2 (or steps 2+3) in a cache and key it by media path. That saves the cloud round-trip — a real win on cloud storage — but every render still pays for the parse, scope, and stringify work (steps 4–7). Locally, where the IO was already cheap, this barely moves the needle.

**"Cache the fully-rendered output including per-call attributes."** Tempting, but the cache key now has to include width, height, *and* alt-text. A logo used in three different layouts (with three different size combinations) explodes into three cache entries. Worse, alt-text in particular is often dynamic (`alt="@Model.SiteName"`), which means a hit is mostly accidental.

**The version that does work**: cache the post-scope SVG markup — *without* the per-call attributes baked in — keyed by media path only. Then on each render, do a quick parse-and-attribute-inject step on top. The cache key stays simple, the cached blob is reusable across every call site, and the only per-render cost is the (cheap) attribute mutation.

This is the version we landed on. It hinges on the fact that the scope class derives from the media path, so the cached blob is correct for every consumer of the same SVG.

## Our approach

Umbraco ships an in-process cache — `Umbraco.Cms.Core.Cache.AppCaches` — with three policies: `RuntimeCache` (in-memory, per-app), `RequestCache` (per-request), and `IsolatedCaches` (named, longer-lived). For our use case `RuntimeCache` is the right one: SVGs are tiny, shared across all requests, and we want them to outlive any single request.

`RuntimeCache.GetCacheItem<T>(key, factory, timeout)` is the API. If the key is in the cache, return the value; otherwise call the factory, store the result with the given TTL, and return it. The factory is invoked atomically per key, so concurrent first-render requests don't all race to read the same SVG.

We wrap the read + sanitise + scope work (everything except per-call attribute injection) in `GetCacheItem` and pick a 60-minute TTL.

## Walkthrough

The change is small — about a dozen lines on top of the scoping refinement. The file is still [`src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`](../../../src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs).

### Step 1 — Inject `AppCaches`

Add the dependency to the constructor. Umbraco registers `AppCaches` in DI by default; no `Program.cs` changes needed.

```csharp
using Umbraco.Cms.Core.Cache;

public class SvgTagHelper : TagHelper
{
    private readonly IPublishedUrlProvider _urlProvider;
    private readonly MediaFileManager _mediaFileManager;
    private readonly AppCaches _appCaches;  // ← new
    private readonly ILogger<SvgTagHelper> _logger;

    private static readonly TimeSpan SvgCacheTtl = TimeSpan.FromMinutes(60);

    public SvgTagHelper(
        MediaFileManager mediaFileManager,
        IPublishedUrlProvider urlProvider,
        AppCaches appCaches,                  // ← new
        ILogger<SvgTagHelper> logger)
    {
        _mediaFileManager = mediaFileManager;
        _urlProvider = urlProvider;
        _appCaches = appCaches;
        _logger = logger;
    }
}
```

`SvgCacheTtl` is hardcoded for now. The upstream `Our.Umbraco.TagHelpers` package exposes it as a per-call attribute (`cache-minutes`) and an app-wide config setting; we don't have a use case for either yet but the option is open.

### Step 2 — Extract read + sanitise + scope into a helper

The expensive work moves into its own method so the cache factory can call it directly. The method returns `null` if the file is missing or empty, which the cache will faithfully cache (a missing SVG stays missing for the TTL — see the trade-offs section).

```csharp
private string? ReadSanitiseAndScope(string mediaItemPath)
{
    if (_mediaFileManager.FileSystem.FileExists(mediaItemPath) == false)
    {
        return null;
    }

    var fileStream = _mediaFileManager.FileSystem.OpenFile(mediaItemPath);
    using var reader = new StreamReader(fileStream);
    var contents = reader.ReadToEnd();

    contents = Regex.Replace(contents, @"<script.*?script>", string.Empty,
        RegexOptions.IgnoreCase | RegexOptions.Singleline);
    contents = Regex.Replace(contents, @"javascript:", @"syntax:error:",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    if (string.IsNullOrEmpty(contents)) return null;

    var doc = new HtmlDocument();
    try
    {
        doc.LoadHtml(contents);
        var svgs = doc.DocumentNode.SelectNodes("//svg");
        if (svgs != null && svgs.Count > 0)
        {
            var scopeClass = GetScopeClass(mediaItemPath);
            foreach (var svgNode in svgs)
            {
                ScopeInlineStyles(svgNode, scopeClass);
            }
            return doc.DocumentNode.OuterHtml;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing svg for html output");
        return null;
    }

    return contents;
}
```

Notice this method *doesn't* know about width/height/alt-text. Those are per-call concerns and stay in `Process`.

### Step 3 — Wrap the helper in `GetCacheItem`

In `Process`, after the media-URL and extension check, the body becomes a one-liner:

```csharp
var scopedSvg = _appCaches.RuntimeCache.GetCacheItem<string?>(
    $"svg-scoped::{mediaItemPath}",
    () => ReadSanitiseAndScope(mediaItemPath),
    timeout: SvgCacheTtl);

if (string.IsNullOrEmpty(scopedSvg))
{
    output.SuppressOutput();
    return;
}
```

The `svg-scoped::` prefix on the cache key is a soft namespace — it stops the key from colliding with any other cached item in the same `RuntimeCache` instance. You'll see this convention in a lot of Umbraco code.

### Step 4 — Skip the per-call parse when nothing needs injecting

The hot path is callers like `Logo.cshtml` that don't pass width/height/alt-text. For those, the cached blob is already exactly what we want — we can hand it straight to the response and never touch HtmlAgilityPack.

```csharp
output.Attributes.RemoveAll("media");

// Fast path: no per-call attributes, emit the cached output directly.
if (!Width.HasValue && !Height.HasValue && !AltText.HasValue())
{
    output.TagName = null;
    output.Content.SetHtmlContent(scopedSvg);
    return;
}

// Slow path: parse the cached markup, inject the per-call attributes,
// and emit. We re-parse rather than try to string-edit the SVG because
// HtmlAgilityPack handles edge cases (multi-line tags, existing
// attributes, attribute escaping) correctly.
HtmlDocument doc = new HtmlDocument();
try
{
    doc.LoadHtml(scopedSvg);
    var svgs = doc.DocumentNode.SelectNodes("//svg");
    if (svgs != null && svgs.Count > 0)
    {
        foreach (var svgNode in svgs)
        {
            if (Width.HasValue)  svgNode.SetAttributeValue("width", Width.Value.ToString());
            if (Height.HasValue) svgNode.SetAttributeValue("height", Height.Value.ToString());
            if (AltText.HasValue()) svgNode.SetAttributeValue("alt", AltText);
        }
        scopedSvg = doc.DocumentNode.OuterHtml;
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error injecting attributes into svg output");
}

output.TagName = null;
output.Content.SetHtmlContent(scopedSvg);
```

The fast path matters more than it looks. The two logos in this site's header and footer hit it on every page render; the result is that after a single warm-up render, those SVGs cost essentially nothing.

### What the cache hit costs on the fast path

After warm-up: one in-memory dictionary lookup, one null-check, one `SetHtmlContent` call. Microseconds. Compared to a fresh render's ~5ms parse + scope work on local media (or ~50ms+ per blob round-trip on cloud), the speedup is roughly two orders of magnitude.

## Alternatives we considered

- **Cache only the sanitised string (skip step 5+).** Saves the IO and the regex passes, but every render still parses and scopes. About half the win for slightly less code. Worth it if you can't move scope-class generation to be deterministic and need *something*.
- **`IMemoryCache` directly.** ASP.NET Core's standard memory-cache abstraction would work, but bypassing `AppCaches` gives up Umbraco's integration with the rest of the cache surface — including the cache-clear lifecycle (e.g. when an admin clicks "Clear application cache" in the backoffice). For TagHelpers that read Umbraco media, `AppCaches` is the right layer.
- **Invalidate on Umbraco's `MediaSaved` notification.** The 60-minute TTL means an editor who replaces an SVG file at the same media path will see the old version cached for up to 60 minutes. We could subscribe to `MediaSavedNotification` and evict the relevant cache entries on save, eliminating the stale window. We haven't done it because the cost (a notification handler that imports `INotificationHandler<MediaSavedNotification>`, plus knowing which cache keys to evict from a `MediaWithCrops` instance) outweighs the benefit for our edit cadence — but if your editors are routinely swapping SVGs in place during the day, it's a small extra layer to add.
- **Per-call cache opt-in (mirroring upstream).** `Our.Umbraco.TagHelpers`' `<our-svg>` accepts `cache="true" cache-minutes="120"` as per-call attributes, plus a global `IgnoreAppSettings` override. We made caching always-on with a fixed TTL because there's no scenario in our codebase where we'd want to *bypass* the cache, and the per-attribute machinery adds API surface for no payoff. If you're building a more general-purpose package, the upstream pattern is more flexible.

## Trade-offs and known limits

- **Stale content for up to one TTL after a media replace.** If an editor uploads a new file at the same media path, the cache will keep serving the old SVG until the entry expires (or until the app restarts, or until the backoffice's "Clear application cache" button is pressed). Same behaviour as upstream's caching, just with a different opt-in shape. Bump the TTL down, hook the `MediaSaved` notification, or expose `cache-minutes` as an attribute if this bites.
- **Negative caching is cheap.** A missing file or a parse failure also caches (as `null` for one TTL), which means a typo'd media reference won't hammer the storage layer on every render. If you'd rather retry — for instance, because you're racing a deployment that hasn't published the file yet — change the factory to return a sentinel and only cache successful results.
- **No bound on cache size.** `RuntimeCache` doesn't enforce a memory limit out of the box. For our use case (a media library of a few dozen SVGs, each a few KB) this isn't a concern, but a site with thousands of distinct inline SVGs across the page surface would want to keep an eye on memory or move to `IsolatedCaches` with explicit eviction policy.
- **Fixed TTL.** Currently hardcoded to 60 minutes via `SvgCacheTtl`. If you need per-deployment control, lift it to an `IOptions<…>`-bound config setting and read it in the constructor.
- **Per-call attributes still re-parse.** On the slow path (any caller passing width/height/alt-text), every render still does an HtmlAgilityPack parse to mutate the SVG root. That's fast (microseconds for a small SVG) but it's the upper limit of what this caching layer can save. If a hot page is dominated by SVG calls with varying widths, you'd need either smarter parsing or a different design (e.g. setting width/height on a wrapper element instead of the `<svg>` itself).

## Where this leaves us

The TagHelper now does three things, each layered cleanly on the previous one:

1. **Foundation** — inline an SVG from Umbraco media, with attribute injection and security sanitisation.
2. **Scoping** — scope the SVG's inline `<style>` so class names don't bleed across SVGs on the same page.
3. **Caching** — serve repeated renders of the same SVG from `RuntimeCache`, skipping the read/parse/scope work for everything after the first.

For a page with ten inline SVGs on cloud-backed media, that goes from ten round-trips and ten parse cycles per render to effectively zero, once warm. The render-path overhead of `<svg-src>` becomes negligible.

The full implementation is at [`src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`](../../../src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs).

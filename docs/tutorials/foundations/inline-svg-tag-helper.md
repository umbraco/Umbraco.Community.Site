---
tags: [svg, tag-helper, razor, media]
---

# Building an inline SVG TagHelper for Umbraco

This tutorial walks through the `<svg-src>` TagHelper that the Umbraco Community site uses to inline SVG files from media into views. It's a *foundation* piece — most of the SVG-related tutorials in this suite build on top of it.

## Credit where it's due

The code shown here is **adapted from [`InlineSvgTagHelper`](https://github.com/umbraco-community/Our-Umbraco-TagHelpers/blob/main/Our.Umbraco.TagHelpers/InlineSvgTagHelper.cs)** in the [Our.Umbraco.TagHelpers](https://github.com/umbraco-community/Our-Umbraco-TagHelpers) community project, MIT-licensed.

Created by **[Warren Buckley](https://github.com/warrenbuckley)**, with later contributions from **[AndyBoot](https://github.com/AndyBoot)** (caching + viewBox enforcement), **[drpeck](https://github.com/drpeck)** (exception handling), and **[profcinders](https://github.com/profcinders)**.

The version in this repo is reshaped for the Umbraco Community site — media-only source, security sanitisation, and per-SVG `<style>` scoping — but the inlining approach and the use of HtmlAgilityPack are theirs. If you're starting a new project, **install `Our.Umbraco.TagHelpers` from NuGet first** and only fork from it if you need behaviour the package doesn't already give you.

## Why you might want an inline SVG TagHelper

You have three obvious ways to put an SVG on a page:

1. **`<img src="/media/.../logo.svg">`** — works, but the browser sandboxes the SVG. You can't reach inside it with CSS to recolour paths, animate on hover, or react to dark-mode preferences. Good for content imagery, wrong for icons and brand marks.
2. **`background-image: url(/media/.../logo.svg)`** — same limitations as `<img>`, plus you give up the SVG's intrinsic aspect ratio and need to manage dimensions in CSS.
3. **Inline `<svg>...</svg>` in the HTML** — the entire SVG DOM becomes part of the page. CSS targets paths, JavaScript animates them, screen readers can read embedded `<title>` and `<desc>`. This is what you want for logos, icons, and any decorative artwork that should respond to design tokens.

Inlining by hand means copying the SVG markup into your Razor view, which means the marketing team can't update the logo without a developer in the loop. A TagHelper bridges the two: editors upload an SVG to Umbraco media, the developer writes one line of Razor, and the SVG renders inline.

## What we're building

A custom TagHelper that turns this Razor:

```cshtml
<svg-src media="@Model.Logo" width="120" height="40" alt-text="Umbraco Community"></svg-src>
```

…into this in the response:

```html
<svg viewBox="..." width="120" height="40" alt="Umbraco Community">
  <path .../>
  ...
</svg>
```

Behind the scenes, the TagHelper:

1. Resolves the Umbraco media item to a file path.
2. Reads the SVG file contents from media storage.
3. Strips any embedded `<script>` tags and `javascript:` URLs (defensive — editors upload these files).
4. Parses the markup with [HtmlAgilityPack](https://html-agility-pack.net/) so it can mutate it.
5. Injects the `width`, `height`, and `alt` attributes you passed.
6. Replaces the `<svg-src>` tag with the SVG content.

## How ours differs from upstream

If you've used `Our.Umbraco.TagHelpers` before, here's where this version diverges. None of this is "better" — they're trade-offs for our context.

| | Upstream `<our-svg>` | This repo's `<svg-src>` |
|---|---|---|
| Tag name | `our-svg` | `svg-src` |
| Source attribute | `src` (wwwroot file) **and** `media-item` (Umbraco media) | `media` only (Umbraco media) |
| Caching | `cache`, `cache-minutes`, `ignore-appsettings` attributes (opt-in per call) | Always on; deterministic per media path; 60-minute TTL |
| ViewBox enforcement | `ensure-viewbox` | None |
| Attribute injection | `class` | `width`, `height`, `alt-text` |
| Security pass | — | Strips `<script>` blocks; neutralises `javascript:` URLs |
| Per-SVG `<style>` scoping | — | Yes — see the [scoping tutorial](../refinements/scoping-inline-svg-styles.md) |

This tutorial covers the **foundational** TagHelper — the read-from-media + sanitise + inline pipeline. The repo's `SvgTagHelper.cs` layers scoping and caching on top of that foundation; both are covered as separate refinements so you can adopt them only if/when you need them. If you need wwwroot loading, the upstream package gives it to you out of the box.

## Walkthrough

The full file lives at [`src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`](../../../src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs). We'll build it up in roughly the order Warren did, so you can see what each piece is for.

### Step 1 — Declare the TagHelper

A TagHelper is a class that derives from `Microsoft.AspNetCore.Razor.TagHelpers.TagHelper`, decorated with `[HtmlTargetElement(...)]` to declare the tag name it handles:

```csharp
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace UmbracoCommunity.Web.TagHelpers
{
    [HtmlTargetElement("svg-src")]
    public class SvgTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // We'll fill this in.
        }
    }
}
```

`TagHelperOutput` is your handle to the final rendered HTML — you can set its content, change its tag name, or suppress it entirely.

### Step 2 — Wire up Umbraco services via DI

We need to resolve the Umbraco media URL and open the file from media storage. Both are services Umbraco registers in DI: `IPublishedUrlProvider` for the URL, `MediaFileManager` for the file content.

```csharp
private readonly IPublishedUrlProvider _urlProvider;
private readonly MediaFileManager _mediaFileManager;
private readonly ILogger<SvgTagHelper> _logger;

public SvgTagHelper(
    MediaFileManager mediaFileManager,
    IPublishedUrlProvider urlProvider,
    ILogger<SvgTagHelper> logger)
{
    _mediaFileManager = mediaFileManager;
    _urlProvider = urlProvider;
    _logger = logger;
}
```

TagHelpers are resolved from the request scope, so constructor injection just works — no extra registration code in `Program.cs`.

### Step 3 — Accept the `media` attribute

TagHelpers expose properties as HTML attributes via `[HtmlAttributeName(...)]`:

```csharp
[HtmlAttributeName("media")]
public MediaWithCrops? Media { get; set; }

[HtmlAttributeName("width")]
public int? Width { get; set; }

[HtmlAttributeName("height")]
public int? Height { get; set; }

[HtmlAttributeName("alt-text")]
public string? AltText { get; set; }
```

`MediaWithCrops` is Umbraco's wrapper around `IPublishedContent` that adds crop info. You don't need crops here, but it's the type Umbraco's image-cropper property editor hands you, so accepting it directly means the call site is just `<svg-src media="@Model.Logo">`.

### Step 4 — Bail out early on invalid input

There are four reasons to give up before doing any work: no media passed, the file isn't an SVG, the file doesn't exist, or the contents are empty. Each one calls `output.SuppressOutput()`, which makes the whole `<svg-src>` tag and its content disappear from the response.

```csharp
public override void Process(TagHelperContext context, TagHelperOutput output)
{
    if (Media == null)
    {
        output.SuppressOutput();
        return;
    }

    var mediaItemPath = Media.Url(_urlProvider);
    if (mediaItemPath?.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase) != true)
    {
        output.SuppressOutput();
        return;
    }

    if (_mediaFileManager.FileSystem.FileExists(mediaItemPath) == false)
    {
        output.SuppressOutput();
        return;
    }

    // ...
}
```

Why early-exit instead of throwing? The TagHelper runs during page render. Throwing here yells in the logs *and* takes down the whole page. A missing logo should degrade gracefully — the page renders, just without that one image.

### Step 5 — Read the SVG from media storage

Umbraco abstracts the underlying media filesystem (could be the local disk, Azure Blob Storage, S3, …) behind `MediaFileManager.FileSystem`. Don't reach for `System.IO.File.ReadAllText` — it'll work locally and break in production.

```csharp
var fileStream = _mediaFileManager.FileSystem.OpenFile(mediaItemPath);
using var reader = new StreamReader(fileStream);
var fileContents = reader.ReadToEnd();
```

### Step 6 — Sanitise the content

Editors upload these files. Treat the bytes as untrusted: strip any `<script>` blocks and break `javascript:` URLs.

```csharp
var cleanedFileContents = Regex.Replace(
    fileContents,
    @"<script.*?script>",
    string.Empty,
    RegexOptions.IgnoreCase | RegexOptions.Singleline);

cleanedFileContents = Regex.Replace(
    cleanedFileContents,
    @"javascript:",
    @"syntax:error:",
    RegexOptions.IgnoreCase | RegexOptions.Singleline);

if (string.IsNullOrEmpty(cleanedFileContents))
{
    output.SuppressOutput();
    return;
}
```

This is a deliberately small pass — it doesn't try to be a full XSS sanitiser. If you have any reason to expect adversarial uploads, run the content through something stronger like the [HTML Agility Pack with a sanitiser ruleset](https://github.com/mganss/HtmlSanitizer) before serving it.

### Step 7 — Parse and mutate

Now the interesting bit. We want to add `width`, `height`, and `alt` attributes to the root `<svg>` element. The straightforward way to do that is to parse the markup into a DOM, find the `<svg>` node, and set attributes on it.

```csharp
output.Attributes.RemoveAll("media");

HtmlDocument doc = new HtmlDocument();
try
{
    doc.LoadHtml(cleanedFileContents);
    var svgs = doc.DocumentNode.SelectNodes("//svg");
    if (svgs != null && svgs.Count > 0)
    {
        foreach (var svgNode in svgs)
        {
            if (Width.HasValue)
            {
                svgNode.SetAttributeValue("width", Width.Value.ToString());
            }
            if (Height.HasValue)
            {
                svgNode.SetAttributeValue("height", Height.Value.ToString());
            }
            if (AltText.HasValue())
            {
                svgNode.SetAttributeValue("alt", AltText.ToString());
            }
        }

        cleanedFileContents = doc.DocumentNode.OuterHtml;
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing svg for html output");
}
```

A couple of things to call out:

- **`output.Attributes.RemoveAll("media")`** stops `media=` from leaking onto the rendered tag. Anything you don't remove explicitly will be copied through.
- **`//svg`** is an XPath selector. HtmlAgilityPack uses XPath for `SelectNodes`. In practice there's one `<svg>` per file, but the loop is correct anyway.
- **Try/catch around the parse.** Malformed SVG would otherwise throw and take down the page. drpeck added this in 2024 after a real incident — the original code didn't have it.

### Step 8 — Emit the output

```csharp
output.TagName = null;
output.Content.SetHtmlContent(cleanedFileContents);
```

Setting `TagName = null` removes the outer `<svg-src>...</svg-src>` wrapper entirely. The SVG markup we wrote into `output.Content` becomes the direct child of whatever Razor element the TagHelper was inside.

### Step 9 — Use it from Razor

Once the TagHelper is in the assembly, register it in `Views/_ViewImports.cshtml`:

```cshtml
@addTagHelper *, UmbracoCommunity.Web
```

Now you can call it from any view:

```cshtml
@if (Model.Logo != null)
{
    <span class="logo-container">
        <svg-src media="@Model.Logo" alt-text="@Model.SiteName"></svg-src>
    </span>
}
```

The real use site in this repo is at [`src/UmbracoCommunity.Web.UI/Views/Partials/Components/Logo.cshtml`](../../../src/UmbracoCommunity.Web.UI/Views/Partials/Components/Logo.cshtml).

## Alternatives we considered

- **Use the upstream `Our.Umbraco.TagHelpers` package directly.** This is the right answer if its feature set is enough for you. We forked early in the project's life when we wanted to remove the wwwroot-source branch and add security sanitisation, and the divergence has accrued from there. If you're starting fresh: install the NuGet.
- **Razor partial with `@Html.Raw`.** Works, but you have no hook to mutate the SVG before output — no attribute injection, no security pass, no scoping. Every consumer has to remember to handle those concerns.
- **ViewComponent.** Heavier than a TagHelper for what's essentially a markup transform. ViewComponents are best when there's logic *around* what to render; here, the input and output are both just SVG.

## Trade-offs and known limits

- **Every render reads from media storage.** This minimal version has no caching, so for media backed by local disk it's fast enough but for cloud blob storage it'll add tens of milliseconds per inline SVG per page. The repo's version solves this — see [Caching the scoped SVG output](../refinements/caching-scoped-svg-output.md).
- **No `class` attribute injection.** Upstream's TagHelper accepts `class="..."` and merges it onto the root `<svg>`; ours doesn't. If you need that, mirror the pattern in Step 7: read the existing `class` attribute, concatenate, set.
- **Single-tag-name target.** `[HtmlTargetElement("svg-src")]` binds to one element name. If you wanted both `<our-svg>` and `<svg-src>` to work during a migration, you'd need to decorate the class with the attribute twice (it's repeatable).
- **No XML namespace handling.** HtmlAgilityPack is forgiving about SVG-as-HTML, but if your SVG uses XML namespaces other than the default (`xmlns:dc`, `xmlns:cc`, …) you may see them stripped or normalised in the output. For Illustrator-exported SVGs this doesn't bite.

## Where to go next

If you're using this TagHelper across many SVGs on the same page and you've ever hit the "wrong colour after the logo loaded" bug, the next stop is:

→ [Scoping inline SVG `<style>` to prevent class-name bleed](../refinements/scoping-inline-svg-styles.md)

---
tags: [svg, css-scoping, style-isolation, tag-helper]
---

# Scoping inline SVG `<style>` to prevent class-name bleed

> **Prerequisites:** This tutorial extends an existing inline-SVG TagHelper. If you don't have one yet (or want to understand the one we're modifying), start with [Building an inline SVG TagHelper for Umbraco](../foundations/inline-svg-tag-helper.md). The walkthrough below assumes you've read it.

There's a surprisingly satisfying class of "the logo went the wrong colour" bugs that all trace back to the same root cause: inline `<style>` blocks inside SVGs aren't actually scoped to the SVG they live in, even though every fibre of your developer instinct says they ought to be. This tutorial walks through how we ran into the problem on the Umbraco Community site, why the obvious fixes don't quite hold up, and how we ended up solving it with a small change to a single TagHelper.

## The problem

The site renders SVG logos inline (via a `<svg-src>` TagHelper) so they can be styled and re-coloured from CSS. The header uses the full-colour Umbraco Community logo; the footer uses an all-white variant of the same shape.

Both files were exported from Adobe Illustrator. Open one and you'll see something like this at the top:

```xml
<svg viewBox="...">
  <defs>
    <style>
      .st0 { fill: #f5c1bc; }
      .st1 { fill: #ffffff; }
      .st2 { fill: #1b264f; }
      ...
    </style>
  </defs>
  <path class="st0" .../>
  <path class="st1" .../>
  ...
</svg>
```

`.st0`, `.st1`, … `.stN` are generic class names Illustrator auto-generates on export. They're meaningful only relative to *this* SVG.

The symptom: as soon as the footer logo loaded on the page, the header logo's `.st0` path turned white. Both logos used `.st0`, but their `.st0` rules disagreed on the colour.

## Why the obvious fix doesn't work

The instinctive reaction is to think of an SVG's `<style>` as part of the SVG itself, the way a CSS rule inside a Web Component's Shadow DOM is. **It isn't.** Inline `<style>` in SVG (and in HTML) is document-scoped — the browser adds those rules to the document's global stylesheet list. The last `.st0 { fill: ... }` declared on the page wins for every `.st0` element, regardless of which SVG it happens to sit in. Frustrating, but consistent.

You can patch a single instance with author CSS:

```css
/* header.css */
.logo-container .st0 { fill: #f5c1bc !important; }
```

That works until the next Illustrator export uses a different class-to-path mapping (which it will — class numbering is per-export). Now your `.logo-container .st0` rule paints the wrong shape and you're chasing it with more overrides.

We tried a defensive variant first: a global `svg .st0–.st9 { fill: inherit }` safeguard, with a `.preserve-svg-fills` opt-out wrapper for logos that needed to keep their palette. It worked for the header (the opt-out let the SVG's own `<style>` apply) but the footer logo also had its own `<style>` wanting different colours — and as soon as two SVGs were opted out, they fought each other again. The safeguard was treating the symptom, not the cause.

## Our approach

If the problem is that an SVG's `<style>` selectors are global, make them less global — but do it where the SVG is served, not in the SVG file or the author's CSS.

The `<svg-src>` TagHelper already parses every SVG it inlines (to set width/height/alt attributes). We extended it to do one more thing:

1. Derive a unique class per SVG file — `svg-` plus the first 12 hex chars of a SHA1 of the media path. Same path → same scope class, every time.
2. Add it to the `<svg>` element.
3. Walk every `<style>` block inside the SVG and prefix every selector with `.svg-<hash> `.

The SVG that goes to the browser then looks like this:

```xml
<svg class="svg-a1b2c3d4e5f6" viewBox="...">
  <defs>
    <style>
      .svg-a1b2c3d4e5f6 .st0 { fill: #f5c1bc; }
      .svg-a1b2c3d4e5f6 .st1 { fill: #ffffff; }
      ...
    </style>
  </defs>
  <path class="st0" .../>
  ...
</svg>
```

The selectors are still global cascade-wise, but they can't match `.st0` paths inside *another* SVG — that SVG has a different scope class (different media path → different hash). And because the scope class is deterministic per file, every render of the same SVG produces identical scoped markup, which makes the whole pipeline cacheable — see the caching refinement that builds on this one.

## Walkthrough

### Step 1 — Find where you control SVG output

The community site already has a TagHelper that reads an SVG from media storage and inlines it into the response: `src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`. It already uses [HtmlAgilityPack](https://html-agility-pack.net/) (a forgiving HTML/XML parser for .NET that gives you a navigable DOM-like tree from a string of markup) to parse the SVG, so adding one more parse-and-mutate step on top is essentially free.

If your project doesn't have something like this, you'll need it. The point of this tutorial is the *idea* — scope at serve time, not at author time — which works wherever you control the SVG payload.

### Step 2 — Derive a stable scope class from the media path

We derive the scope class from a hash of the media path, so every render of the same SVG file produces the same scope class:

```csharp
using System.Security.Cryptography;
using System.Text;

private static string GetScopeClass(string mediaPath)
{
    var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(mediaPath));
    return "svg-" + Convert.ToHexString(bytes).Substring(0, 12).ToLowerInvariant();
}
```

Why stable rather than random? Two reasons:

- **Cacheability.** A random `Guid` per render would mean every render produces unique output, which is fine for correctness but defeats any attempt to cache the post-scope markup. With a stable hash, every render of `media/.../logo.svg` produces byte-identical scoped output — the cache layer described in the caching refinement only works because of this property.
- **It's harmless if two SVGs hash to the same class.** They'd both have to be the same media file for the hash to collide (different paths produce different hashes), and two instances of the same SVG sharing a scope class is fine — their internal `<style>` rules are identical, so there's nothing for them to bleed *into* each other.

Then in `SvgTagHelper.Process`, after the existing width/height/alt loop, call a new method per SVG element with the scope class:

```csharp
var scopeClass = GetScopeClass(mediaItemPath);
foreach (var svgNode in svgs)
{
    // … width/height/alt injection as before …
    ScopeInlineStyles(svgNode, scopeClass);
}
```

`ScopeInlineStyles` attaches the class to the SVG and rewrites every `<style>` block it finds:

```csharp
private static void ScopeInlineStyles(HtmlNode svgNode, string scopeClass)
{
    var styleNodes = svgNode.SelectNodes(".//style");
    if (styleNodes == null || styleNodes.Count == 0) return;

    var existing = svgNode.GetAttributeValue("class", string.Empty);
    svgNode.SetAttributeValue("class",
        string.IsNullOrEmpty(existing) ? scopeClass : $"{existing} {scopeClass}");

    foreach (var styleNode in styleNodes)
    {
        styleNode.InnerHtml = PrefixCssSelectors(styleNode.InnerHtml, scopeClass);
    }
}
```

Two small decisions worth flagging:

- **Class, not id.** Illustrator already gives the SVG an `id="Layer_1"`, and some SVGs use that id internally via `<use xlink:href="#Layer_1">`. We add a class instead of overwriting the id, so internal references keep working.
- **Append to existing class.** A few SVGs in the wild already have a class on the root element; don't clobber it.

### Step 3 — Prefix the selectors

Selectors get rewritten with a single regex:

```csharp
private static readonly Regex SelectorRegex = new(
    @"(^|\})\s*(?<sel>[^@{}][^{}]*?)\s*\{",
    RegexOptions.Compiled);

private static string PrefixCssSelectors(string css, string scopeClass) =>
    SelectorRegex.Replace(css, m =>
    {
        var prefixed = string.Join(
            ", ",
            m.Groups["sel"].Value.Split(',').Select(s => $".{scopeClass} {s.Trim()}"));
        return $"{m.Groups[1].Value}{prefixed} {{");
    });
```

What the regex does in plain English: find every position that's either the start of the string or just after a `}`, then capture the run of characters up to the next `{`. That run is the selector list. Split it on commas (to handle multi-selector rules like `.st0, .st1 { … }`), prefix each one with `.{scopeClass} `, and stitch it back.

The `[^@{}]` at the start of the capture is what skips at-rules like `@media` and `@keyframes` — their declarations pass through untouched.

### Step 4 — Decide what becomes deletable

Anything in the author CSS that was defending against bleed is now redundant. In this codebase that meant deleting four things:

| File | What we deleted | Why it was there |
| --- | --- | --- |
| `src/UmbracoCommunity.StaticAssets/src/css/base/base.css` | The `svg .st0–.st9 { fill: inherit }` safeguard | Flattened every `.stN` to inherited text colour so no two SVGs could fight |
| `src/UmbracoCommunity.Web.UI/Views/Partials/Components/Logo.cshtml` | The `.preserve-svg-fills` opt-out class | Opt-out from the safeguard above, for SVGs that wanted to keep their palette |
| `src/UmbracoCommunity.StaticAssets/src/css/layout/header.css` & `header-mobile.css` | `.logo-container .st0`/`.st1` hardcoded fills | Codegarden-era overrides that re-asserted the old logo's two colours; they actively broke the new five-colour logo |
| `src/UmbracoCommunity.StaticAssets/src/css/layout/footer.css` | `footer .st0, .st1 { fill: white !important }` | Forced the codegarden logo all-white in the dark footer; redundant once the footer points at an all-white variant SVG whose own `<style>` scopes correctly |

### Step 5 — Keep the overrides that aren't about bleed

Not every external `.stN` rule was a workaround. The site has hero-mode overrides like:

```css
body.hero-full-width:not(.scroll):not(.mobile) .nav-start .logo-link svg .st1 {
  fill: var(--color-white) !important;
}
```

This one recolours the logo's blue path to white when the logo sits over a dark hero image. It's solving a different problem — *intentional* recolouring per page state, not defending against accidental cross-SVG bleed — and it keeps working under the new scheme. The hero rule sits outside the SVG, so scoping doesn't touch it, and `!important` beats the scoped internal rule on the rare path where they'd disagree.

## Alternatives we considered

- **CSS [`@scope`](https://developer.mozilla.org/en-US/docs/Web/CSS/@scope)** — wrap the SVG's `<style>` contents in `@scope (#unique-id) { ... }`. Native, clean syntax, works in Chrome 118+ / Firefox 128+ / Safari 17.4+ (all from 2024). Equivalent end result; we picked manual prefixing because it works in older browsers and the implementation is the same handful of lines either way.
- **[Shadow DOM](https://developer.mozilla.org/en-US/docs/Web/API/Web_components/Using_shadow_DOM)** — render each SVG inside a custom element with a shadow root. Style encapsulation comes for free. Heavyweight for static SVGs, and breaks any author CSS that currently targets paths *across* the SVG boundary (like the hero override above).
- **Editing each SVG to use unique class names** — find/replace `.st0` → `.community-logo-st0` etc. Works, but you need to do it every time a non-technical user uploads a new SVG through Umbraco, which means it isn't really a solution.
- **Strip the `<style>` block, inline fills onto each path** — defeats the cascade entirely. Loses the per-class hooks that author CSS (and hero overrides) depend on.

## Trade-offs and known limits

- **Regex-based CSS parsing.** The selector regex handles flat rule lists and `@`-rules with bodies. It would mishandle nested at-rules that contain class selectors *inside* them (e.g. `@media (...) { .st0 { ... } }`). Illustrator's export pattern doesn't use those, but if you adopt this in a project with hand-written SVG stylesheets, swap the regex for a proper CSS parser.
- **Every SVG gets parsed.** The TagHelper now always runs through HtmlAgilityPack, even for SVGs without a `<style>` block. SVGs are typically small (a few KB) and parsing is fast, but if you serve thousands of inline SVGs per page you'd want to add a cheap pre-check: skip the parse if the file content doesn't contain the substring `<style`.
- **Scope class collisions are theoretical.** Twelve hex chars from SHA1 gives ~48 bits of entropy; a collision needs two media paths whose hashes happen to share their first 12 hex chars, which is vanishingly unlikely at any realistic media-library size. If you're feeling paranoid, take more hex chars or hash with something wider than SHA1.

## Where to go next

Now that every SVG produces deterministic scoped output, we can cache the result instead of re-doing the read + sanitise + parse + prefix work on every single render. That's the subject of the next refinement:

→ [Caching the scoped SVG output](./caching-scoped-svg-output.md)

The full implementation in this repo includes both layers, side by side — see [`src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs`](../../../src/UmbracoCommunity.Web/TagHelpers/SvgTagHelper.cs).

Hopefully that takes one of those persistent "why on earth is the logo white now?" mysteries off your plate.

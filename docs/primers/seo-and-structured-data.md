---
tags: [primer, seo, schema-org, meta-tags]
---

# SEO and structured data primer

The [content-modelling primer](content-modelling.md#compositions-sharing-fields-without-inheritance) covers `ICompositionSeo` — the mixin that gives a page `MetaTitle`, `MetaDescription`, `OgImage`, `Robots`, and `CustomSchema`. This primer picks up from there: what happens to those fields between an editor filling them in and a `<meta>` tag or JSON-LD `<script>` landing in the rendered page. The whole pipeline funnels through one service and one view, which makes it a short primer — the point is mostly to save you from having to trace it yourself the first time.

> Just want the tag inventory? Skip to [OpenGraph, Twitter Cards, and the Sessionize deep-link override](#opengraph-twitter-cards-and-the-sessionize-deep-link-override).

## The pipeline in one picture

```
Layout.cshtml
  ↓ @await Component.InvokeAsync("MetaTags")
MetaTagsViewComponent          — thin: resolves currentPage, delegates, no logic of its own
  ↓
ISeoDataService.BuildAsync()   — all the assembly happens here
  ↓
MetaTagsViewModel
  ↓
MetaTags.cshtml                — renders every tag: title, OG, Twitter, robots, canonical, JSON-LD
```

Worth being precise about this shape because it's easy to guess wrong: the Open Graph and Twitter Card markup does **not** live in `Layout.cshtml` — the layout only invokes the view component; every tag is rendered by `Views/Shared/Components/MetaTags/MetaTags.cshtml`. If you're hunting for a specific meta tag, that partial is always where to look, and `SeoDataService` is always where its *value* was decided.

## Building the view model: `SeoDataService`

`ISeoDataService.BuildAsync(IPublishedContent currentPage)` is the entire public surface — one method, returning a `MetaTagsViewModel`. Its shape:

1. Seed `MetaTitle` from `currentPage.Name` and `SiteName` from `GetSiteSettings()` (tenant-scoped, per the [multi-tenancy primer](multi-tenancy.md)).
2. **Branch on `currentPage is ICompositionSeo contentModel`** — the same "check the interface, not a doc-type list" idiom the content-modelling primer recommends. If the page composes it: `MetaTitle` falls back to the page name when the editor left it blank, `OpenGraphImageUrl` resolves from `OgImage`, `Robots` and `CanonicalUrl` get filled in, pagination is applied, and the schema builders run.
3. **If the page doesn't compose `ICompositionSeo`** — `DigitalSignagePage` is the one example today — the whole branch is skipped. Only `MetaTitle` gets set. No description, no OG image, no robots, no canonical, no schema. That's deliberate for a kiosk page with no SEO surface, but it's also the thing to remember if you add a new page type and its meta tags mysteriously don't render: check whether it composes `ICompositionSeo` at all.
4. One unconditional special case runs regardless of branch: the `Documentation` node. Because a single content node serves every `/docs/...` URL, its node-derived title/canonical would otherwise collapse every rendered article onto the same `/docs/` URL — so `SeoDataService` overwrites `CanonicalUrl` (built from the live request path), `MetaTitle`, and a 200-character-truncated `MetaDescription` from whichever doc was actually resolved.

The "Page Title | Site Name" title format isn't built here, though — `MetaTitle` and `SiteName` stay separate fields on the view model, and the concatenation happens in `MetaTags.cshtml`'s `<title>` tag. If you ever need to change the title format sitewide, that's the one line to edit.

## Schema builders

`ViewModelBuilders/Schema/` has three builders — `ArticleSchemaBuilder`, `OrganizationSchemaBuilder`, `BreadcrumbSchemaBuilder` — and deliberately no shared interface between them. Each builds a different [Schema.NET](https://github.com/RehanSaeed/Schema.NET) (`13.0.0`, centrally pinned) type from different inputs, so a common `ISchemaBuilder<T>` abstraction wouldn't buy much:

- **`ArticleSchemaBuilder`** — builds `Schema.NET.Article` for `Article` content only (returns `null` otherwise): headline, publish/update dates, author, image, description, and a `Publisher` built by delegating straight to `OrganizationSchemaBuilder`.
- **`OrganizationSchemaBuilder`** — builds `Schema.NET.Organization`, and never returns `null`. This is the one with the tenant-aware fallback: a single `hasCustomSettings` flag gates name/URL/logo *together* — either the tenant's `SocialSettings` are complete enough to use, or all three fields fall back to hardcoded Umbraco defaults. The [tenant-aware schema fallback tutorial](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md) walks through why that's all-or-nothing rather than falling back field-by-field; this primer won't re-derive it.
- **`BreadcrumbSchemaBuilder`** — builds `Schema.NET.BreadcrumbList` by walking `content.Ancestors().Reverse()` down to the current page. Returns `null` for the home page itself (no ancestors, no breadcrumb).

An editor-authored escape hatch sits alongside all three: `ICompositionSeo.CustomSchema` is a raw string that gets added to the page's schema markup untouched — no Schema.NET processing, no validation, straight passthrough. A single article page can end up with several JSON-LD blocks at once (custom schema, Article-or-WebPage, and Breadcrumb, all independently opted in).

Every built object is serialized with Schema.NET's own `.ToHtmlEscapedString()` and collected onto the view model; `MetaTags.cshtml` loops that collection and emits one `<script type="application/ld+json" asp-add-nonce="true">` per entry — the `asp-add-nonce` is what keeps JSON-LD compliant with the site's CSP (see `NonceTagHelper` in the [backend primer](backend.md)).

## OpenGraph, Twitter Cards, and the Sessionize deep-link override

`MetaTags.cshtml` emits, in order: `og:title`/`twitter:title`, `description`/`og:description`/`twitter:description`, a hardcoded `og:type` of `"website"` (there's no `article` branch, even for `Article` pages — worth knowing if you ever go looking for one and can't find it), `og:site_name`, `og:locale`, `twitter:card` (`summary_large_image`), a hardcoded `twitter:site` handle, then conditionally `og:image`/`twitter:image` (with explicit `1200×628` dimensions) and the canonical/prev/next links covered below.

Sessionize session sharing overrides several of those tags server-side for one specific case: a URL visited with `?session={sessionId}` (see the Sessionize section of [CLAUDE.md](../../CLAUDE.md) for the deep-linking feature this serves). The override lives inline in `SeoDataService` as a private method, `ApplySessionOpenGraphOverridesAsync` — if you've seen a `SeoMetaDataViewModelDecorator` mentioned elsewhere, that class doesn't exist; this method is the real implementation. It looks up the session via the same `SessionizeApiClient` the Sessionize feature already uses, and on a hit overwrites `MetaTitle` (`"{session title} by {speakers}"`), `MetaDescription`, and appends `?session={id}` onto the canonical/`og:url` — so a link shared to LinkedIn, Bluesky, or Mastodon unfurls with that session's own preview instead of the generic program page's. A failed lookup is swallowed silently and the page's default tags stand.

## Canonical URLs and pagination

Both are computed in `SeoDataService`, not the view. `CanonicalUrl` is built from the page's Umbraco URL made absolute against the *current request's* scheme and host — which is what makes canonical URLs automatically tenant-correct with no extra branching: whichever domain served the request is the domain the canonical URL points back at.

Pagination reads a `?page=` query parameter and, when present, sets `PrevUrl` (omitting the query string entirely when linking back to page 1) and `NextUrl`, then — after computing both — overwrites `CanonicalUrl` itself to include the current `?page=` value, so the canonical tag for page 3 of a listing points at page 3, not page 1. `MetaTags.cshtml` renders `rel="canonical"`, `rel="prev"`, and `rel="next"` link tags, each only when the corresponding value is non-empty.

## Robots directives

`ICompositionSeo.Robots` is a free-ish string field, and `MetaTags.cshtml` matches it with exact string equality against a small known set: empty in development renders `noindex, nofollow` as a blanket safety net; empty in production renders a permissive default (`index, follow` plus rich-snippet directives); `"noindex, follow"` and `"noindex, nofollow"` render as-is; `"index, nofollow"` gets the same rich-snippet directives appended as the default case. Anything else — a typo, an unexpected value — falls through every branch and **emits no robots tag at all**, silently. If you're adding a new robots option, it needs its own branch here or it'll be a no-op.

## Sitemap generation

`SitemapController` (a plain `RenderController` behind a 60-second response cache) resolves `context.PublishedRequest?.PublishedContent?.Root()` as the sitemap's starting node — which means the sitemap is **per-tenant by construction**, not one combined document: a request to a given domain's `/sitemap.xml` only ever walks that domain's own subtree, the same way every other tenant-scoped lookup in this codebase works.

`ContentDataService.GetSitemap` walks the tree with two independent checks:
- **`HideFromSitemap`** (from `ICompositionPageConfiguration`, the sibling of `HideFromSearch`) skips the entire branch when set — children are never visited, so it's inherited by exclusion.
- A node is only emitted as a `<url>` entry when it has a template *and* composes `ICompositionPageConfiguration` — so non-routable structural nodes (a Blog year/month folder, say) are traversed but never listed themselves, while their routable descendants (articles) still are.

The output is a bare-bones `sitemaps.org/schemas/sitemap/0.9` document — `<loc>` and `<lastmod>` only, no `changefreq`/`priority`.

## Where to go next

- **[Content modelling primer](content-modelling.md)** — `ICompositionSeo` and the other compositions this pipeline consumes.
- **[Tenant-aware fallback for schema and SEO metadata](../tutorials/refinements/tenant-fallback-for-schema-and-seo.md)** — the full walkthrough of `OrganizationSchemaBuilder`'s all-or-nothing fallback, including alternatives considered.
- **[Multi-tenancy primer](multi-tenancy.md)** — why canonical URLs and sitemaps come out tenant-correct without any SEO-specific tenant logic.
- **[Backend primer](backend.md)** — where `MetaTagsViewComponent` sits among the other layout-slot ViewComponents, and the CSP nonce system JSON-LD scripts rely on.

That's the whole pipeline, start to finish — welcome aboard!

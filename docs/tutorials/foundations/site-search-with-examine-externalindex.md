---
tags: [search, examine, multi-tenant, lucene]
---

# Site search backed by Umbraco's Examine ExternalIndex

"How do I add search to my Umbraco site?" is a perennial community question, and most answers stop at the single-tenant happy path: query `ExternalIndex`, render the hits, done. This tutorial walks through what that answer actually looks like once you add a second tenant, a second content source, and a page full of results that need to paginate cleanly — the site search on the Umbraco Community site, built entirely on Umbraco's own zero-config Examine index. It's a *foundation* piece: nothing else in this suite builds on it, but it's a self-contained answer to a question that comes up on every multi-page Umbraco site sooner or later.

## Why you might want this

Umbraco already indexes every published page into [Examine](https://shazwazza.github.io/Examine/)'s `ExternalIndex` the moment you install it — no composer, no config section, no custom field definitions. The tempting shortcut is to assume that's the whole job: query the index, show the hits. It nearly is, for a single-tenant site. The moment you run several tenants from one Umbraco instance (see the [multi-tenancy primer](../../primers/multi-tenancy.md)), a naive query returns hits from every tenant's content, and a search on Site A leaks Site B's pages — the exact class of bug that primer's "one rule" exists to prevent, now showing up inside a Lucene query instead of a view-model builder.

## What we're building

One typed service, `SearchService`, sitting behind `ISearchService`, doing four things:

1. **Query `ExternalIndex`** — Umbraco's own default index, entirely unconfigured in this repo — using Examine's *managed* query API rather than raw Lucene syntax.
2. **Scope to the current tenant**, in code, after the query runs.
3. **Merge in a second, unrelated index** — the community blog feed — so results can span "this tenant's own pages" and "aggregated external content" in one ranked list.
4. **Paginate and excerpt** the combined, filtered set for a render controller to hand to a view.

## Why the obvious fix doesn't work

**Not filtering by tenant at all.** The most obvious version of this feature is "query the index, show every hit." On a single-tenant site that's correct. On this site it's the multi-tenancy bug in its most literal form: Site B's article shows up in Site A's search results because Examine's `ExternalIndex` is one flat index across every tenant's content — nothing about the index itself knows tenants exist.

**Building the query as a raw Lucene string.** If you want control over which fields match and how much each one counts, Examine's `NativeQuery` API is the obvious reach — you write actual Lucene query syntax, exactly like the codebase's own [`DocumentationSearchService`](../../../src/UmbracoCommunity.Web/Services/Documentation/Search/DocumentationSearchService.cs) does for the `/docs` section, with field-boost syntax like `title^10`. The cost is that whatever the visitor typed becomes part of a query language with its own special characters (`+`, `-`, `"`, `*`, parentheses…), so you either escape user input by hand or accept odd failures when someone searches for `C++` or `"quoted"`. `SearchService` sidesteps the whole problem by using `ManagedQuery` instead — an analysed, tokenised match against a field list, not a query string the visitor can break.

**Paginating by asking Examine to skip/take the final page directly.** The obvious shape for "page 2 of results" is `.Execute(QueryOptions.SkipTake(10, 10))` and trust the count Examine reports. It doesn't survive contact with post-query filtering: this service drops hits after the fact (wrong tenant, the page you're already on, non-routable content), so a raw Examine-level skip/take would page over the *unfiltered* set and produce wrong totals and gaps. The code's own comment on this is direct: fetch a bounded batch, "apply tenant + current-page filtering in memory so pagination totals stay accurate after filtering."

## Walkthrough

### Step 1 — There's nothing to set up

[`SearchService.cs`](../../../src/UmbracoCommunity.Web/Services/SearchService.cs) reaches for the index by name — `Umbraco.Cms.Core.Constants.UmbracoIndexes.ExternalIndexName` — and that's the only mention of `ExternalIndex` anywhere in this codebase. No composer registers it, no `appsettings.json` section configures it, no custom field definitions or `IValueSetValidator` populate it. Umbraco's core indexing pipeline does that automatically for every published node the moment the site boots, which is the whole reason this is worth knowing about: contrast it with the two indexes this same repo *does* build by hand — [`CommunityBlogsIndex`](../../../src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsSearchIndexer.cs) (indexing aggregated external blog posts that don't live in Umbraco's content tree at all, so it needs its own populator) and `DocumentationLuceneIndex` (indexing markdown files). `ExternalIndex` needs none of that scaffolding because the content it indexes already lives in Umbraco.

### Step 2 — Query with `ManagedQuery`, not `NativeQuery`

```csharp
searchResults = index.Searcher
    .CreateQuery("content")
    .ManagedQuery(query, SearchFields)
    .Not().Field("templateID", "0")
    .Execute(QueryOptions.SkipTake(0, MaxIndexFetch));
```

`SearchFields` is a fixed list — `nodeName`, `metaTitle`, `metaDescription`, `teaser`, `bannerContent`, `contentBlocks` — deliberately scoped to author-facing content fields, with a code comment explaining why: so editor identity fields like `writerName`/`creatorName` and other system metadata never surface as a match. `.Not().Field("templateID", "0")` excludes non-routable nodes (folders, settings nodes — anything without a template) at the Examine level, before a single result ever reaches C#. `MaxIndexFetch` (500) bounds the raw fetch; see Step 3 for why the real pagination happens after this, not here.

### Step 3 — Scope to the tenant, in memory, and say why

```csharp
var tenantRootId = currentPage.Root().Id;
...
foreach (var result in searchResults)
{
    if (!int.TryParse(result.Id, out var id)) continue;

    var content = umbracoContext.Content.GetById(id);
    if (content is null) continue;
    if (content.Root().Id != tenantRootId) continue;
    if (content.Id == currentPage.Id) continue;
    // Only routable pages whose doc type opts into the page-config composition AND
    // that haven't been flagged hideFromSearch are searchable. Doing this in code (vs
    // in the Examine query) sidesteps the fact that nodes without the composition
    // simply don't have the hideFromSearch field indexed at all.
    if (!content.TemplateId.HasValue) continue;
    if (content is not ICompositionPageConfiguration pageConfig) continue;
    if (pageConfig.HideFromSearch) continue;
    ...
}
```

This is the same `currentPage.Root()` idiom the [multi-tenancy primer](../../primers/multi-tenancy.md) documents everywhere else in this codebase — no Examine-specific tenancy mechanism, just the ordinary helper, applied per hit after resolving the Examine document ID back to a real `IPublishedContent` via the published content cache. The `HideFromSearch` check has to happen here rather than as an Examine field filter for exactly the reason the comment states: `HideFromSearch` only exists on content types that compose `ICompositionPageConfiguration` (see the [content-modelling primer](../../primers/content-modelling.md#compositions-sharing-fields-without-inheritance)), so nodes that don't compose it were never indexed with that field at all — there's nothing for an Examine query to filter on.

Worth knowing before you lean on this: because the tenant/visibility filtering happens *after* a capped 500-document fetch, a search term popular enough to return more than 500 raw hits across *every* tenant combined could drop legitimate same-tenant matches that ranked below that cutoff, before tenant filtering even runs. That's a real trade-off of filtering in memory rather than in the index — acceptable while result volumes stay well under the cap, worth revisiting if they don't.

### Step 4 — Merge in a second, unrelated index

```csharp
if (_examineManager.TryGetIndex(CommunityBlogsSearchIndexer.IndexName, out var communityIndex))
{
    var communityResults = communityIndex.Searcher
        .CreateQuery()
        .ManagedQuery(query, CommunitySearchFields)
        .Execute(QueryOptions.SkipTake(0, MaxIndexFetch));
    // ...mapped into the same combined list, IsExternal = true
}

// Merge content + community hits by raw Lucene score. Note: scores from two different
// indexes are not strictly comparable (different IDF/field norms/doc counts), so the
// interleave is approximate by design — acceptable for this feed's size.
var ordered = combined.OrderByDescending(x => x.Score).ToList();
```

The community blog index is deliberately *not* tenant-filtered — it's global content, marked `IsExternal = true` on the result so the view can badge it differently. Sorting both sources together by raw Lucene `Score` is a pragmatic choice the code names honestly in its own comment: scores from two different indexes aren't calibrated against each other (different document counts and field statistics mean the same score doesn't mean the same relevance), so the interleaved order is approximate, not a rigorous cross-index ranking. Good enough at this feed's size; worth re-examining if either source grows enough for the approximation to start showing.

### Step 5 — Excerpts: strip, don't parse

```csharp
private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

private static string? BuildExcerpt(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return null;
    var text = WebUtility.HtmlDecode(HtmlTagRegex.Replace(raw, " "));
    text = WhitespaceRegex.Replace(text, " ").Trim();
    if (text.Length == 0) return null;
    if (text.Length <= ExcerptMaxChars) return text;
    return text[..ExcerptMaxChars].TrimEnd() + "…";
}
```

A plain regex tag-strip, not an HTML parser — reasonable for well-formed Umbraco rich text, not a hardened sanitiser (it doesn't need to be; this runs against your own indexed content, not arbitrary untrusted markup). `WebUtility.HtmlDecode` un-escapes entities left over after stripping, then the excerpt is capped at 200 characters with an ellipsis. The source field is picked by priority — `metaDescription`, then `teaser`, then `contentBlocks`, first non-empty wins. There's no query-term highlighting; every excerpt is truncated uniformly regardless of where the match actually falls in the text.

### Step 6 — The controller: read, clamp, don't re-run

[`SearchPageController.cs`](../../../src/UmbracoCommunity.Web/Controllers/Render/SearchPageController.cs):

```csharp
var query = (Request.Query["q"].ToString() ?? string.Empty).Trim();
var page = ParsePage(Request.Query["page"].ToString());
...
if (viewModel.HasQuery)
{
    var skip = (page - 1) * PageSize;
    var (results, total) = await _searchService.SearchAsync(currentPage, query, skip, PageSize, cancellationToken);
    viewModel.Results = results;
    viewModel.TotalResults = total;

    if (viewModel.TotalPages > 0 && page > viewModel.TotalPages)
    {
        viewModel.CurrentPage = viewModel.TotalPages;
    }
}
```

`q` is `.Trim()`'d and nothing else — no length cap, no character filtering — which is fine precisely because `ManagedQuery` (Step 2) never treats it as anything but analysed text. `ParsePage` falls back to page 1 on anything that doesn't parse as a positive integer. One quirk worth knowing if you're debugging an empty results page that says "Page 3 of 3": the clamp at the end only corrects the *displayed* page number after an out-of-range request — it doesn't re-run the search at the corrected `skip`, so a request for page 47 of a 3-page result set renders an empty list captioned "Page 3 of 3," not page 3's actual results.

### Step 7 — Two forms, one page, no autocomplete

The nav search icon ([`Menu.cshtml`](../../../src/UmbracoCommunity.Web.UI/Views/Shared/Components/Menu/Menu.cshtml)) and the search page itself ([`SearchPage.cshtml`](../../../src/UmbracoCommunity.Web.UI/Views/SearchPage.cshtml)) are both plain `<form method="GET">`s posting a `q` parameter straight to `SearchPageController` — a full page load, no fetch call, no results dropdown. (The `/docs` section has its own separate as-you-type widget over its own separate index; that's a different feature entirely, not a more advanced version of this one.)

The search page's input is a good example of a pattern worth reusing anywhere you'd otherwise write `autofocus`:

```html
<input type="search" id="search-page-input" name="q" value="@Model.Query" placeholder="Search" />
...
<script asp-add-nonce="true">
    document.getElementById("search-page-input")?.focus({ preventScroll: true });
</script>
```

Plain `autofocus` makes the browser scroll the focused element into view on load — on a page with anything above the input, that's a jarring scroll-jump the instant the page renders. `Element.focus({ preventScroll: true })` focuses the field without forcing that scroll. (See the [nonce CSP tutorial](nonce-csp-with-razor.md) for what `asp-add-nonce` is doing on that `<script>` tag.)

## Alternatives we considered

- **Filtering by an Examine `path`/ancestor field instead of `currentPage.Root()` in code.** Examine's default schema does expose a `path` field, and a query-level ancestor filter would scope tenancy inside the index itself — no 500-document cap to worry about, no per-hit content-cache lookup. The shipped approach trades that efficiency for consistency: it's the exact same `Root()` helper every other tenant-scoped lookup in this codebase already uses (per the multi-tenancy primer), rather than a second, Examine-specific way of expressing the same rule.
- **`NativeQuery` with field boosting**, the way `DocumentationSearchService` does it for the curated `/docs` index. More ranking control, at the cost of hand-sanitising arbitrary public input against Lucene syntax — a reasonable trade for a curated markdown index, a riskier one for open-ended search against everything editors publish.
- **An as-you-type dropdown**, matching the docs section's own widget. Not built here — full-page-load search is simpler, with no client-side fetch/debounce/results-panel code to maintain for what this site needed.

## Trade-offs and known limits

- **The 500-document fetch cap plus in-memory filtering** (Step 3) — a popular-enough term across all tenants combined could silently drop same-tenant hits.
- **Cross-index score comparison is approximate**, by the code's own admission — ordering between tenant-content hits and community-blog hits isn't a calibrated relevance ranking.
- **No query-term highlighting** in excerpts.
- **No automated tests** for `SearchService` or `SearchPageController`.

Two things this tutorial found and fixed rather than just documented: `SeoDataService.AddPageQuery` — the mechanism the [SEO primer](../../primers/seo-and-structured-data.md#canonical-urls-and-pagination) documents — used to build `<link rel="canonical">`/`prev`/`next` tags from `?page=` alone, silently dropping every other query parameter (`?q=` on this exact page, on `/search?q=umbraco&page=2`) from all three. It now preserves them. And `ISearchService`'s XML doc comment used to claim the implementation was "backed by Umbraco.AI.Search" — a copy-pasted artifact from an unrelated service; it's corrected to describe the real Examine-based implementation.

## Where to go next

- **[Multi-tenancy primer](../../primers/multi-tenancy.md)** — the `Root()` pattern this service leans on, and the class of bug it exists to prevent.
- **[Content modelling primer](../../primers/content-modelling.md)** — `ICompositionPageConfiguration` and why `HideFromSearch` can't always be filtered at the index level.
- **[SEO and structured data primer](../../primers/seo-and-structured-data.md)** — the pagination/canonical mechanism this page's `?q=` fix (Trade-offs, above) landed in.

Hopefully that's the version of "add search to your Umbraco site" that survives a second tenant — welcome aboard!

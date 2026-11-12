---
tags: [search, examine, multi-tenant, lucene]
---

# How to add multi-tenant site search using Umbraco's built-in Examine index

"How do I add search to my Umbraco site?" is a perennial community question, and most answers stop at the single-tenant happy path: query `ExternalIndex`, render the hits, done. This tutorial walks through what that answer actually looks like once you add a second tenant, a second content source, and a page full of results that need to paginate cleanly — the site search on the Umbraco Community site, built entirely on Umbraco's own zero-config Examine index. It's a *foundation* piece: nothing else in this suite builds on it, but it's a self-contained answer to a question that comes up on every multi-page Umbraco site sooner or later.

## Why you might want this

Umbraco already indexes every published page into [Examine](https://shazwazza.github.io/Examine/)'s `ExternalIndex` the moment you install it — no composer, no config section, no custom field definitions. The tempting shortcut is to assume that's the whole job: query the index, show the hits. It nearly is, for a single-tenant site. The moment you run several tenants from one Umbraco instance (see the [multi-tenancy primer](../../primers/multi-tenancy.md)), a naive query returns hits from every tenant's content, and a search on Site A leaks Site B's pages — the exact class of bug that primer's "one rule" exists to prevent, now showing up inside a Lucene query instead of a view-model builder.

## What we're building

One typed service, `SearchService`, sitting behind `ISearchService`, doing four things:

1. **Query `ExternalIndex`** — Umbraco's own default index, entirely unconfigured in this repo — matching each word exactly, by its root and with small typos, through a Lucene query built only from the words the visitor typed.
2. **Scope to the current tenant**, in code, after the query runs.
3. **Merge in two more, unrelated indexes** — the community blog feed and the documentation site's own Lucene index — so results can span "this tenant's own pages," "aggregated external content," and "primers/tutorials" in one ranked list.
4. **Paginate and excerpt** the combined, filtered set for a render controller to hand to a view.

## Why the obvious fix doesn't work

**Not filtering by tenant at all.** The most obvious version of this feature is "query the index, show every hit." On a single-tenant site that's correct. On this site it's the multi-tenancy bug in its most literal form: Site B's article shows up in Site A's search results because Examine's `ExternalIndex` is one flat index across every tenant's content — nothing about the index itself knows tenants exist.

**Building the query as a raw Lucene string.** If you want control over which fields match and how much each one counts, Examine's `NativeQuery` API is the obvious reach — you write actual Lucene query syntax, exactly like the codebase's own [`DocumentationSearchService`](../../../src/UmbracoCommunity.Web/Services/Documentation/Search/DocumentationSearchService.cs) does for the `/docs` section, with field-boost syntax like `title^10`. The cost is that whatever the visitor typed becomes part of a query language with its own special characters (`+`, `-`, `"`, `*`, parentheses…), so you either escape user input by hand or accept odd failures when someone searches for `C++` or `"quoted"`. `SearchService` does use `NativeQuery`, because exact-only matching (`ManagedQuery`) can't find "Speakers" for "who is speaking" or "Tickets" for "tickts" — but it never passes the visitor's text through. It splits the query into words of letters and digits only and builds the Lucene string from those (Step 2), so there's nothing left to escape. A query with no usable words, such as `C++`, falls back to `ManagedQuery`.

**Paginating by asking Examine to skip/take the final page directly.** The obvious shape for "page 2 of results" is `.Execute(QueryOptions.SkipTake(10, 10))` and trust the count Examine reports. It doesn't survive contact with post-query filtering: this service drops hits after the fact (wrong tenant, the page you're already on, non-routable content), so a raw Examine-level skip/take would page over the *unfiltered* set and produce wrong totals and gaps. The code's own comment on this is direct: fetch a bounded batch, "apply tenant + current-page filtering in memory so pagination totals stay accurate after filtering."

## Walkthrough

### Step 1 — There's nothing to set up

[`SearchService.cs`](../../../src/UmbracoCommunity.Web/Services/SearchService.cs) reaches for the index by name — `Umbraco.Cms.Core.Constants.UmbracoIndexes.ExternalIndexName` — and that's the only mention of `ExternalIndex` anywhere in this codebase. No composer registers it, no `appsettings.json` section configures it, no custom field definitions or `IValueSetValidator` populate it. Umbraco's core indexing pipeline does that automatically for every published node the moment the site boots, which is the whole reason this is worth knowing about: contrast it with the two indexes this same repo *does* build by hand — [`CommunityBlogsIndex`](../../../src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsSearchIndexer.cs) (indexing aggregated external blog posts that don't live in Umbraco's content tree at all, so it needs its own populator) and `DocumentationLuceneIndex` (indexing markdown files). `ExternalIndex` needs none of that scaffolding because the content it indexes already lives in Umbraco.

### Step 2 — Build the Lucene query from words, not from what was typed

```csharp
searchResults = Match(index.Searcher.CreateQuery("content"), query, SearchFields, "nodeName", ContentFuzzyFields)
    .Not().Field("templateID", "0")
    .Execute(QueryOptions.SkipTake(0, MaxIndexFetch));

private static IBooleanOperation Match(IQuery examineQuery, string query, string[] fields, string titleField, string[] fuzzyFields)
{
    var lucene = SearchQueryBuilder.Build(query, fields, titleField, fuzzyFields);
    return lucene is null ? examineQuery.ManagedQuery(query, fields) : examineQuery.NativeQuery(lucene);
}
```

[`SearchQueryBuilder`](../../../src/UmbracoCommunity.Web/Services/SearchQueryBuilder.cs) turns "Who is speaking?" into Lucene clauses like this:

```text
nodeName:speaking^9  nodeName:speak*^4.5  nodeName:speaking~2^1.5
contentBlocks:speaking^3  contentBlocks:speak*^1.5  ...
```

- **Words:** split on anything that isn't a letter or digit, lower-cased and accent-folded (`København` → `kobenhavn`, matching the index analyzer's ASCII folding). Filler words ("who", "is", "the"…) and single letters are dropped, and at most six words are used.
- **Exact match** (boost 3) ranks highest, so precise hits still lead.
- **Root match** (boost 1.5): a light English stem strips common endings, so `speaking`/`speakers` → `speak*` and `tickets` → `ticket*`.
- **Typo match** (boost 0.5) for words of 5+ letters — one edit up to 7 letters, two from 8 — but only on short, title-like fields (`nodeName`, `metaTitle`; blog and docs titles), because fuzzy matching is the costliest clause on long body fields.
- **Titles count three times more** than body fields, so a page *called* "Speakers" beats a page that mentions speaking.

The same builder is used for the community blog and documentation queries in Step 4, each with its own title and typo fields.

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
    if (!content.IsSearchable()) continue;
    ...
}
```

This is the same `currentPage.Root()` idiom the [multi-tenancy primer](../../primers/multi-tenancy.md) documents everywhere else in this codebase — no Examine-specific tenancy mechanism, just the ordinary helper, applied per hit after resolving the Examine document ID back to a real `IPublishedContent` via the published content cache. `IsSearchable()` ([`PublishedContentExtensions`](../../../src/UmbracoCommunity.Web/Extensions/PublishedContentExtensions.cs)) is the single definition of a searchable page — routable, composing `ICompositionPageConfiguration`, and not flagged `HideFromSearch` — shared with the AI suggestions in Step 8. The `HideFromSearch` check has to happen in code rather than as an Examine field filter because `HideFromSearch` only exists on content types that compose `ICompositionPageConfiguration` (see the [content-modelling primer](../../primers/content-modelling.md#compositions-sharing-fields-without-inheritance)), so nodes that don't compose it were never indexed with that field at all — there's nothing for an Examine query to filter on.

Worth knowing before you lean on this: because the tenant/visibility filtering happens *after* a capped 500-document fetch, a search term popular enough to return more than 500 raw hits across *every* tenant combined could drop legitimate same-tenant matches that ranked below that cutoff, before tenant filtering even runs. That's a real trade-off of filtering in memory rather than in the index — acceptable while result volumes stay well under the cap, worth revisiting if they don't.

### Step 4 — Merge in two more, unrelated indexes

```csharp
if (_examineManager.TryGetIndex(CommunityBlogsSearchIndexer.IndexName, out var communityIndex))
{
    var communityResults = Match(communityIndex.Searcher.CreateQuery(), query, CommunitySearchFields, CommunityBlogsSearchIndexer.FieldTitle, CommunityFuzzyFields)
        .Execute(QueryOptions.SkipTake(0, MaxIndexFetch));
    // ...mapped into the same combined list, IsExternal = true
}

if (_examineManager.TryGetIndex(DocumentationLuceneIndex.IndexName, out var docsIndex))
{
    var docsRoot = currentPage.Root().DescendantsOrSelf<Models.PublishedModels.Documentation>().FirstOrDefault();
    if (docsRoot is not null)
    {
        var docsResults = Match(docsIndex.Searcher.CreateQuery(), query, DocumentationSearchFields, DocumentationIndexFields.Title, DocumentationFuzzyFields)
            .Execute(QueryOptions.SkipTake(0, MaxIndexFetch));
        // ...mapped into the same combined list, IsExternal = false
    }
}

// Merge content + community + documentation hits by raw Lucene score. Note: scores from different
// indexes are not strictly comparable (different IDF/field norms/doc counts), so the
// interleave is approximate by design — acceptable for this feed's size.
var ordered = combined.OrderByDescending(x => x.Score).ToList();
```

The community blog index is deliberately *not* tenant-filtered — it's global content, marked `IsExternal = true` on the result so the view can badge it differently. The one exception: the aggregate also carries the site's own blog RSS, and those articles already come back as `ExternalIndex` content hits, so community hits whose URL host is listed in `CommunityBlogs:ExcludedListingHosts` are skipped in memory before the combined set is counted and paged. Documentation articles are the opposite case: same-site content that just doesn't live in Umbraco's content tree, so it's marked `IsExternal = false` but still needs tenant scoping — the code resolves the current tenant's single `Documentation` node the same way [`DocumentationContentFinder`](../../../src/UmbracoCommunity.Web/Routing/DocumentationContentFinder.cs) does, then prefixes that node's URL onto the article's stored path (which is relative to the docs root, with no tenant prefix baked in). A missing index or an unresolvable `Documentation` node for the tenant just contributes nothing, the same fail-open shape as the community blog merge.

Sorting all three sources together by raw Lucene `Score` is a pragmatic choice the code names honestly in its own comment: scores from different indexes aren't calibrated against each other (different document counts and field statistics mean the same score doesn't mean the same relevance), so the interleaved order is approximate, not a rigorous cross-index ranking. Good enough at this feed's size; worth re-examining if any source grows enough for the approximation to start showing.

Both merges use the same word-built query as the tenant-content query in Step 2, so a typo or word form matches blog posts and docs articles the same way it matches pages. The docs section's own search box ([`DocumentationSearchService`](../../../src/UmbracoCommunity.Web/Services/Documentation/Search/DocumentationSearchService.cs)) still has its separate boosted query.

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

### Step 6 — The controller: read, clamp, re-run

[`SearchPageController.cs`](../../../src/UmbracoCommunity.Web/Controllers/Render/SearchPageController.cs):

```csharp
var (results, total) = await _searchService.SearchAsync(currentPage, query, (page - 1) * PageSize, PageSize, cancellationToken);
viewModel.TotalResults = total;

if (viewModel.TotalPages > 0 && page > viewModel.TotalPages)
{
    viewModel.CurrentPage = viewModel.TotalPages;
    (results, total) = await _searchService.SearchAsync(currentPage, query, (viewModel.CurrentPage - 1) * PageSize, PageSize, cancellationToken);
}
```

`q` is `.Trim()`'d and nothing else — safe because Step 2 never puts the raw text into a Lucene string. `ParsePage` falls back to page 1 on anything that doesn't parse as a positive integer. An out-of-range page (page 47 of a 3-page result set) is clamped to the last page and the search re-runs for it, so the visitor sees page 3's results rather than an empty list. The search is logged for the Search Terms dashboard only when page 1 was requested, and suggestions (Step 8) only run on page 1.

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

### Step 8 — When keyword search finds little, ask the AI search

Keyword search can only find pages that contain the visitor's words. When it finds little, the search page asks Umbraco.AI.Search, which matches by meaning:

- **No results:** "Perhaps one of these will help?" with up to 4 pages.
- **1–3 results:** a "Related results" section below them with up to 3 pages that aren't already in the results (`SearchPageController.SelectRelated`).
- **4+ results:** nothing extra.

Both use [`ContentSuggestionService`](../../../src/UmbracoCommunity.Web/Services/ContentSuggestionService.cs), the same service behind the 404 page's "Perhaps it was one of these?" ([`PageNotFoundSuggestionService`](../../../src/UmbracoCommunity.Web/Services/PageNotFoundSuggestionService.cs) only turns the missing URL into words and filters scanner probes). Both pages render the cards with the shared [`PageSuggestions.cshtml`](../../../src/UmbracoCommunity.Web.UI/Views/Partials/Components/PageSuggestions.cshtml) partial and `dc-page-suggestions` styles.

`ContentSuggestionService` keeps the paid embedding calls in check:

- **One cached lookup per tenant, culture and query** (eight page ids, trimmed to what each caller asks for). Hits are cached for a day, "nothing found" for an hour, failures for a minute — see the [caching primer](../../primers/caching.md).
- **A budget on cache misses** ([`SuggestionBudget`](../../../src/UmbracoCommunity.Web/Services/SuggestionBudget.cs)): bots get none (the same user-agent rules as the Search Terms tracker), each visitor gets 20 new lookups an hour, and the whole site 60 a minute. Over budget, the page simply shows no suggestions.
- **At most three concurrent searches**, shedding extra requests rather than queueing them.
- **Overfetching:** media and other tenants' pages share the AI index, so it asks for 40 candidates and keeps this tenant's searchable pages.

Suggestions only appear if the AI search is configured (a default *Embedding* profile and a built index — see Umbraco.AI's settings) and something clears its own relevance cut-off (`MinScore`). That's why "Related results" is sometimes missing: nothing else was similar enough, and the section stays hidden rather than padding itself with weak matches.

## Alternatives we considered

- **Filtering by an Examine `path`/ancestor field instead of `currentPage.Root()` in code.** Examine's default schema does expose a `path` field, and a query-level ancestor filter would scope tenancy inside the index itself — no 500-document cap to worry about, no per-hit content-cache lookup. The shipped approach trades that efficiency for consistency: it's the exact same `Root()` helper every other tenant-scoped lookup in this codebase already uses (per the multi-tenancy primer), rather than a second, Examine-specific way of expressing the same rule.
- **`ManagedQuery` only**, which is what this search originally used. Safe with any input, but exact-word only: no word forms, no typo tolerance, no title weighting. Replaced by Step 2's word-built `NativeQuery`, which keeps the safety by never passing the raw text through.
- **A stemming analyzer on the index** instead of root matching at query time. It would mean customising Umbraco's `ExternalIndex` and re-indexing, and a Porter stemmer still maps "speaking" and "speakers" to different stems.
- **AI search for every query.** Better at meaning, but a paid call per search and weaker for exact names; it's used only when keyword search finds little (Step 8).
- **An as-you-type dropdown**, matching the docs section's own widget. Not built here — full-page-load search is simpler, with no client-side fetch/debounce/results-panel code to maintain for what this site needed.

## Trade-offs and known limits

- **The 500-document fetch cap plus in-memory filtering** (Step 3) — a popular-enough term across all tenants combined could silently drop same-tenant hits.
- **Cross-index score comparison is approximate**, by the code's own admission — ordering between tenant-content, community-blog, and documentation hits isn't a calibrated relevance ranking.
- **No query-term highlighting** in excerpts.
- **Looser matching finds more, weaker results.** A typo-matched common word ("umbrco" → "umbraco") can match most of the site; exact and title matches still rank first. The Search Terms dashboard is the place to spot queries that need tuning.
- **Keyword results aren't cached**, so each search re-runs three Lucene queries.
- **Tests cover the pieces, not the whole:** `SearchQueryBuilder`, the suggestion filtering, budget and related-results selection have unit tests; `SearchService` against a real index doesn't.

## Where to go next

- **[Multi-tenancy primer](../../primers/multi-tenancy.md)** — the `Root()` pattern this service leans on, and the class of bug it exists to prevent.
- **[Content modelling primer](../../primers/content-modelling.md)** — `ICompositionPageConfiguration` and why `HideFromSearch` can't always be filtered at the index level.
- **[SEO and structured data primer](../../primers/seo-and-structured-data.md#canonical-urls-and-pagination)** — the pagination/canonical mechanism this page's results feed into, including how it keeps `?q=` and other query parameters intact across `PrevUrl`/`NextUrl`/`CanonicalUrl`.

Hopefully that's the version of "add search to your Umbraco site" that survives a second tenant — welcome aboard!

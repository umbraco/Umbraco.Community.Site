using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Search.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Services;

internal sealed class PageNotFoundSuggestionService : IPageNotFoundSuggestionService
{
    private static readonly string IndexAlias = AISearchConstants.IndexAliases.Search;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(1);

    private readonly ISearcherResolver _searcherResolver;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PageNotFoundSuggestionService> _logger;

    public PageNotFoundSuggestionService(
        ISearcherResolver searcherResolver,
        IUmbracoContextAccessor umbracoContextAccessor,
        IVariationContextAccessor variationContextAccessor,
        IMemoryCache cache,
        ILogger<PageNotFoundSuggestionService> logger)
    {
        _searcherResolver = searcherResolver;
        _umbracoContextAccessor = umbracoContextAccessor;
        _variationContextAccessor = variationContextAccessor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IPublishedContent>> GetSuggestionsAsync(
        IPublishedContent currentPage,
        string requestedPath,
        string? referrerUrl,
        int max,
        CancellationToken ct)
    {
        if (max <= 0) return Array.Empty<IPublishedContent>();

        var query = BuildQuery(requestedPath, referrerUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogInformation("PageNotFound suggestions: empty query for path '{Path}' (no usable tokens)", requestedPath);
            return Array.Empty<IPublishedContent>();
        }

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) || umbracoContext.Content is null)
        {
            _logger.LogWarning("PageNotFound suggestions: UmbracoContext.Content unavailable — cannot resolve search hits");
            return Array.Empty<IPublishedContent>();
        }

        var tenantRootId = currentPage.Root().Id;
        var culture = _variationContextAccessor.VariationContext?.Culture;

        // Cache the resolved suggestion IDs (not IPublishedContent — those are tied to the
        // request-scoped UmbracoContext and can't safely outlive it). Bots/scrapers re-hit
        // the same 404 URLs constantly; this turns repeat calls into in-memory lookups.
        var cacheKey = $"PageNotFound:{tenantRootId}:{culture}:{max}:{query}";
        var ids = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await ResolveSuggestionIdsAsync(query, currentPage, tenantRootId, max, ct)
                ?? Array.Empty<int>();
        }) ?? Array.Empty<int>();

        if (ids.Count == 0) return Array.Empty<IPublishedContent>();

        var suggestions = new List<IPublishedContent>(ids.Count);
        foreach (var id in ids)
        {
            var content = umbracoContext.Content.GetById(id);
            if (content is null) continue;          // content was unpublished/deleted since cache write
            if (content.Id == currentPage.Id) continue;
            if (!IsRoutableAndSearchable(content)) continue;
            suggestions.Add(content);
        }

        return suggestions;
    }

    private async Task<IReadOnlyList<int>?> ResolveSuggestionIdsAsync(
        string query,
        IPublishedContent currentPage,
        int tenantRootId,
        int max,
        CancellationToken ct)
    {
        var searcher = _searcherResolver.GetSearcher(IndexAlias);
        if (searcher is null)
        {
            _logger.LogInformation("PageNotFound suggestions: no searcher registered for index '{Index}' — install + configure Umbraco.AI.Search", IndexAlias);
            return Array.Empty<int>();
        }

        Umbraco.Cms.Search.Core.Models.Searching.SearchResult result;
        try
        {
            result = await searcher.SearchAsync(
                indexAlias: IndexAlias,
                query: query,
                culture: _variationContextAccessor.VariationContext?.Culture,
                // Overfetch — we still need to skip results outside this tenant's tree.
                skip: 0,
                take: Math.Max(max * 3, 10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI search failed for query '{Query}'", query);
            return null; // don't cache failures
        }

        _logger.LogInformation("PageNotFound suggestions: index='{Index}' query='{Query}' returned {Total} match(es)",
            IndexAlias, query, result.Total);

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) || umbracoContext.Content is null)
        {
            return Array.Empty<int>();
        }

        var ids = new List<int>(max);
        var inspected = 0;
        var droppedNotDocument = 0;
        var droppedNotFound = 0;
        var droppedOtherTenant = 0;
        var droppedHideFromSearch = 0;

        foreach (var doc in result.Documents)
        {
            inspected++;
            if (ids.Count >= max) break;
            if (doc.ObjectType != UmbracoObjectTypes.Document) { droppedNotDocument++; continue; }

            var content = umbracoContext.Content.GetById(doc.Id);
            if (content is null) { droppedNotFound++; continue; }
            if (content.Id == currentPage.Id) continue;          // never suggest the 404 page itself
            if (content.Root().Id != tenantRootId) { droppedOtherTenant++; continue; }
            if (!IsRoutableAndSearchable(content)) { droppedHideFromSearch++; continue; }
            if (ids.Contains(content.Id)) continue;

            ids.Add(content.Id);
        }

        _logger.LogInformation(
            "PageNotFound suggestions: kept {Kept}/{Inspected} (notDocument={NotDocument}, notFound={NotFound}, otherTenant={OtherTenant}, hideFromSearch={HideFromSearch})",
            ids.Count, inspected, droppedNotDocument, droppedNotFound, droppedOtherTenant, droppedHideFromSearch);

        return ids;
    }

    // Mirrors the site-search gating: only routable pages whose doc type opts into the
    // page-config composition AND has not been flagged hideFromSearch are suggestable.
    private static bool IsRoutableAndSearchable(IPublishedContent content)
        => content.TemplateId.HasValue
           && content is ICompositionPageConfiguration pageConfig
           && !pageConfig.HideFromSearch;

    private static string BuildQuery(string requestedPath, string? referrerUrl)
    {
        var tokens = new List<string>();
        tokens.AddRange(TokensFromPath(requestedPath));

        if (!string.IsNullOrEmpty(referrerUrl)
            && Uri.TryCreate(referrerUrl, UriKind.Absolute, out var referrerUri))
        {
            tokens.AddRange(TokensFromPath(referrerUri.AbsolutePath));
        }

        return string.Join(' ', tokens.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> TokensFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) yield break;
        foreach (var raw in path.Split(['/', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries))
        {
            // Skip very short fragments and pure-numeric segments — they add noise to embeddings.
            if (raw.Length <= 1) continue;
            if (raw.All(char.IsDigit)) continue;
            yield return raw;
        }
    }
}

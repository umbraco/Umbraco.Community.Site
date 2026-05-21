using Microsoft.Extensions.Logging;
using Umbraco.AI.Search.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Abstract.Services;

namespace UmbracoCommunity.Web.Services;

internal sealed class PageNotFoundSuggestionService : IPageNotFoundSuggestionService
{
    private static readonly string IndexAlias = AISearchConstants.IndexAliases.Search;

    private readonly ISearcherResolver _searcherResolver;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly ILogger<PageNotFoundSuggestionService> _logger;

    public PageNotFoundSuggestionService(
        ISearcherResolver searcherResolver,
        IUmbracoContextAccessor umbracoContextAccessor,
        IVariationContextAccessor variationContextAccessor,
        ILogger<PageNotFoundSuggestionService> logger)
    {
        _searcherResolver = searcherResolver;
        _umbracoContextAccessor = umbracoContextAccessor;
        _variationContextAccessor = variationContextAccessor;
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

        var searcher = _searcherResolver.GetSearcher(IndexAlias);
        if (searcher is null)
        {
            _logger.LogInformation("PageNotFound suggestions: no searcher registered for index '{Index}' — install + configure Umbraco.AI.Search", IndexAlias);
            return Array.Empty<IPublishedContent>();
        }

        var query = BuildQuery(requestedPath, referrerUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogInformation("PageNotFound suggestions: empty query for path '{Path}' (no usable tokens)", requestedPath);
            return Array.Empty<IPublishedContent>();
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
            _logger.LogWarning(ex, "AI search failed for path '{Path}' query '{Query}'", requestedPath, query);
            return Array.Empty<IPublishedContent>();
        }

        _logger.LogInformation("PageNotFound suggestions: index='{Index}' query='{Query}' returned {Total} match(es)",
            IndexAlias, query, result.Total);

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) || umbracoContext.Content is null)
        {
            _logger.LogWarning("PageNotFound suggestions: UmbracoContext.Content unavailable — cannot resolve search hits");
            return Array.Empty<IPublishedContent>();
        }

        var tenantRootId = currentPage.Root().Id;
        var suggestions = new List<IPublishedContent>(max);
        var inspected = 0;
        var droppedNotDocument = 0;
        var droppedNotFound = 0;
        var droppedOtherTenant = 0;

        foreach (var doc in result.Documents)
        {
            inspected++;
            if (suggestions.Count >= max) break;
            if (doc.ObjectType != UmbracoObjectTypes.Document) { droppedNotDocument++; continue; }

            var content = umbracoContext.Content.GetById(doc.Id);
            if (content is null) { droppedNotFound++; continue; }
            if (content.Id == currentPage.Id) continue;          // never suggest the 404 page itself
            if (content.Root().Id != tenantRootId) { droppedOtherTenant++; continue; }
            if (suggestions.Any(s => s.Id == content.Id)) continue;

            suggestions.Add(content);
        }

        _logger.LogInformation(
            "PageNotFound suggestions: kept {Kept}/{Inspected} (notDocument={NotDocument}, notFound={NotFound}, otherTenant={OtherTenant})",
            suggestions.Count, inspected, droppedNotDocument, droppedNotFound, droppedOtherTenant);

        return suggestions;
    }

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

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Search.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Search.Core.Services;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Extensions;

namespace UmbracoCommunity.Web.Services;

internal sealed partial class ContentSuggestionService : IContentSuggestionService
{
    private static readonly string IndexAlias = AISearchConstants.IndexAliases.Search;

    // A hit is stable; "no matches" can change when content is published; a failure must self-heal quickly.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan EmptyCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan FailureCacheTtl = TimeSpan.FromMinutes(1);

    // Bursts of 404s or searches must not fan out into unbounded vector searches; extra ones get no suggestions.
    private const int MaxConcurrentSearches = 3;
    private static readonly TimeSpan SearchGateWait = TimeSpan.FromMilliseconds(250);
    private static readonly SemaphoreSlim SearchGate = new(MaxConcurrentSearches, MaxConcurrentSearches);

    internal const int MaxQueryLength = 200;

    // One cached lookup per query serves every caller, whatever number of suggestions it asks for.
    internal const int CachedSuggestionCount = 8;

    private readonly ISearcherResolver _searcherResolver;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISuggestionBudget _budget;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ContentSuggestionService> _logger;

    public ContentSuggestionService(
        ISearcherResolver searcherResolver,
        IUmbracoContextAccessor umbracoContextAccessor,
        IVariationContextAccessor variationContextAccessor,
        IHttpContextAccessor httpContextAccessor,
        ISuggestionBudget budget,
        IMemoryCache cache,
        ILogger<ContentSuggestionService> logger)
    {
        _searcherResolver = searcherResolver;
        _umbracoContextAccessor = umbracoContextAccessor;
        _variationContextAccessor = variationContextAccessor;
        _httpContextAccessor = httpContextAccessor;
        _budget = budget;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IPublishedContent>> GetSuggestionsAsync(
        IPublishedContent currentPage,
        string query,
        int max,
        CancellationToken ct)
    {
        var normalisedQuery = NormaliseQuery(query);
        if (max <= 0 || normalisedQuery is null) return Array.Empty<IPublishedContent>();

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) || umbracoContext.Content is null)
        {
            _logger.LogWarning("Content suggestions: UmbracoContext.Content unavailable, cannot resolve search hits");
            return Array.Empty<IPublishedContent>();
        }

        var tenantRootId = currentPage.Root().Id;
        var culture = _variationContextAccessor.VariationContext?.Culture;

        // Cache ids rather than IPublishedContent, which is tied to the request's UmbracoContext.
        var cacheKey = $"ContentSuggestions:{tenantRootId}:{culture}:{normalisedQuery}";
        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<int>? ids) || ids is null)
        {
            if (!_budget.TryConsume(_httpContextAccessor.HttpContext))
            {
                return Array.Empty<IPublishedContent>();
            }

            var resolved = await ResolveSuggestionIdsAsync(normalisedQuery, tenantRootId, CachedSuggestionCount, ct);
            var (value, ttl) = resolved switch
            {
                { Count: > 0 } => (resolved, CacheTtl),
                not null => (resolved, EmptyCacheTtl),
                null => ((IReadOnlyList<int>)Array.Empty<int>(), FailureCacheTtl),
            };
            _cache.Set(cacheKey, value, ttl);
            ids = value;
        }

        if (ids.Count == 0) return Array.Empty<IPublishedContent>();

        return ids
            .Where(id => id != currentPage.Id)
            .Select(id => umbracoContext.Content.GetById(id))
            .OfType<IPublishedContent>()
            .Where(content => content.IsSearchable())
            .Take(max)
            .ToList();
    }

    // Collapses whitespace and caps the length so long pasted text can't make an expensive embedding.
    internal static string? NormaliseQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var text = WhitespaceRegex().Replace(query, " ").Trim().ToLowerInvariant();
        if (text.Length > MaxQueryLength) text = text[..MaxQueryLength].TrimEnd();
        return text.Length == 0 ? null : text;
    }

    private async Task<IReadOnlyList<int>?> ResolveSuggestionIdsAsync(
        string query,
        int tenantRootId,
        int count,
        CancellationToken ct)
    {
        var searcher = _searcherResolver.GetSearcher(IndexAlias);
        if (searcher is null)
        {
            _logger.LogDebug("Content suggestions: no searcher registered for index '{Index}', install and configure Umbraco.AI.Search", IndexAlias);
            return Array.Empty<int>();
        }

        if (!await SearchGate.WaitAsync(SearchGateWait, ct))
        {
            _logger.LogWarning(
                "Content suggestions: concurrency limit ({Limit}) reached, shedding search for query '{Query}'",
                MaxConcurrentSearches, query);
            return null;
        }

        Umbraco.Cms.Search.Core.Models.Searching.SearchResult result;
        try
        {
            result = await searcher.SearchAsync(
                indexAlias: IndexAlias,
                query: query,
                culture: _variationContextAccessor.VariationContext?.Culture,
                // Media and other sites' pages share the index, so overfetch to leave enough of this site's pages.
                skip: 0,
                take: Math.Max(count * 5, 40));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI search failed for query '{Query}'", query);
            return null;
        }
        finally
        {
            SearchGate.Release();
        }

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) || umbracoContext.Content is null)
        {
            return Array.Empty<int>();
        }

        var candidates = result.Documents
            .Where(doc => doc.ObjectType == UmbracoObjectTypes.Document)
            .Select(doc => umbracoContext.Content.GetById(doc.Id))
            .OfType<IPublishedContent>()
            .Select(content => new SuggestionCandidate(content.Id, content.Root().Id, content.IsSearchable()));
        var ids = SelectSuggestionIds(candidates, tenantRootId, count);

        _logger.LogDebug("Content suggestions: query '{Query}' matched {Total}, kept {Kept}", query, result.Total, ids.Count);
        return ids;
    }

    internal static IReadOnlyList<int> SelectSuggestionIds(IEnumerable<SuggestionCandidate> candidates, int tenantRootId, int count)
        => candidates
            .Where(c => c.RootId == tenantRootId && c.IsSearchable)
            .Select(c => c.Id)
            .Distinct()
            .Take(count)
            .ToList();

    internal sealed record SuggestionCandidate(int Id, int RootId, bool IsSearchable);

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

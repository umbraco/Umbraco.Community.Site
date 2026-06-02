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

    // Cache TTLs by outcome. A hit is stable, so cache it long. A clean "no matches" recovers
    // if content is later published, so cache it medium. A transient failure/load-shed must
    // self-heal quickly and must never poison the cache for a full day, so cache it briefly.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan EmptyCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan FailureCacheTtl = TimeSpan.FromMinutes(1);

    // Cap concurrent vector searches process-wide. A 404 storm (scanners) would otherwise fan
    // out into hundreds of simultaneous searches; if the embedding store falls back to
    // brute-force, that exhausts app memory and the SQL connection pool. Shedding load here
    // (returning no suggestions) keeps the site responding.
    private const int MaxConcurrentSearches = 3;
    private static readonly TimeSpan SearchGateWait = TimeSpan.FromMilliseconds(250);
    private static readonly SemaphoreSlim SearchGate = new(MaxConcurrentSearches, MaxConcurrentSearches);

    // Non-content file extensions commonly probed by bots and vulnerability scanners. Real
    // Umbraco content URLs are extensionless, so a 404 ending in one of these is never a
    // mistyped content page — skip the semantic search for it entirely.
    private static readonly HashSet<string> NonContentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".php", ".php5", ".php7", ".asp", ".aspx", ".jsp", ".jspx", ".cgi", ".pl", ".py", ".rb",
        ".sh", ".env", ".ini", ".conf", ".config", ".yml", ".yaml", ".json", ".xml", ".sql",
        ".bak", ".old", ".swp", ".tar", ".gz", ".rar", ".7z", ".log", ".db", ".git",
        // NOTE: ".zip" is intentionally NOT blocked — /seed/latest.zip is a real public
        // endpoint (SeedController) referenced by the contributor build script.
    };

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

        // Scanner probes (e.g. /wp-login.php, /.env, /admin.aspx) still render the 404 page,
        // so without this gate each one would trigger a vector search. Skip them up front.
        if (HasNonContentExtension(requestedPath))
        {
            return Array.Empty<IPublishedContent>();
        }

        var query = BuildQuery(requestedPath, referrerUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogDebug("PageNotFound suggestions: empty query for path '{Path}' (no usable tokens)", requestedPath);
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
        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<int>? ids) || ids is null)
        {
            // null  => transient failure or load-shed: cache briefly so repeat probes stay cheap
            //          but a real outage self-heals within FailureCacheTtl.
            // empty => searched OK, genuinely no matches: medium TTL.
            // hits  => long TTL.
            var resolved = await ResolveSuggestionIdsAsync(query, currentPage, tenantRootId, max, ct);
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
            _logger.LogDebug("PageNotFound suggestions: no searcher registered for index '{Index}' — install + configure Umbraco.AI.Search", IndexAlias);
            return Array.Empty<int>();
        }

        // Shed load rather than pile onto a struggling search store. Returning null is treated
        // as a transient outcome (short-TTL cache), so the URL is retried once traffic subsides.
        if (!await SearchGate.WaitAsync(SearchGateWait, ct))
        {
            _logger.LogWarning(
                "PageNotFound suggestions: concurrency limit ({Limit}) reached, shedding search for query '{Query}'",
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
                // Overfetch — we still need to skip results outside this tenant's tree.
                skip: 0,
                take: Math.Max(max * 3, 10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI search failed for query '{Query}'", query);
            return null; // transient — short-TTL cache, retried later
        }
        finally
        {
            SearchGate.Release();
        }

        _logger.LogDebug("PageNotFound suggestions: index='{Index}' query='{Query}' returned {Total} match(es)",
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

        _logger.LogDebug(
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

    // True when the last path segment carries a non-content file extension (e.g. ".php",
    // ".env"). Umbraco content URLs are extensionless, so these are scanner probes, not
    // mistyped pages. Internal for unit testing.
    internal static bool HasNonContentExtension(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        var lastSlash = path.LastIndexOf('/');
        var lastSegment = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;

        var dot = lastSegment.LastIndexOf('.');
        // No dot, or a trailing dot with nothing after it → no usable extension.
        // (dot == 0 is allowed: dotfiles like ".env" are themselves the extension.)
        if (dot < 0 || dot == lastSegment.Length - 1) return false;

        return NonContentExtensions.Contains(lastSegment[dot..]);
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

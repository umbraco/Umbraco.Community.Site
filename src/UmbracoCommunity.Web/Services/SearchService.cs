using Examine;
using Examine.Search;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Services;

internal sealed class SearchService : ISearchService
{
    private const int ExcerptMaxChars = 200;
    private const int MaxIndexFetch = 500;
    private static readonly string IndexName = Umbraco.Cms.Core.Constants.UmbracoIndexes.ExternalIndexName;

    // Scope the managed query to author-content fields so editor identity
    // (writerName/creatorName) and other system metadata don't surface as hits.
    private static readonly string[] SearchFields =
    {
        "nodeName",
        "metaTitle",
        "metaDescription",
        "teaser",
        "bannerContent",
        "contentBlocks",
    };
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly IExamineManager _examineManager;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IExamineManager examineManager,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        ILogger<SearchService> logger)
    {
        _examineManager = examineManager;
        _umbracoContextAccessor = umbracoContextAccessor;
        _publishedUrlProvider = publishedUrlProvider;
        _logger = logger;
    }

    public Task<(IReadOnlyList<SearchResultItem> Results, int Total)> SearchAsync(
        IPublishedContent currentPage,
        string query,
        int skip,
        int take,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || take <= 0)
        {
            return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((Array.Empty<SearchResultItem>(), 0));
        }

        if (skip < 0) skip = 0;

        if (!_examineManager.TryGetIndex(IndexName, out var index))
        {
            _logger.LogWarning("Search: Examine index '{Index}' not found", IndexName);
            return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((Array.Empty<SearchResultItem>(), 0));
        }

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) || umbracoContext.Content is null)
        {
            return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((Array.Empty<SearchResultItem>(), 0));
        }

        var tenantRootId = currentPage.Root().Id;

        ISearchResults? searchResults;
        try
        {
            // ManagedQuery searches the default analyzed text fields across all indexed properties.
            // Exclude pages flagged hideFromSearch and any node without a template (non-routable).
            // Fetch up to MaxIndexFetch and apply tenant + current-page filtering in memory so
            // pagination totals stay accurate after filtering.
            searchResults = index.Searcher
                .CreateQuery("content")
                .ManagedQuery(query, SearchFields)
                .Not().Field("hideFromSearch", "1")
                .Not().Field("templateID", "0")
                .Execute(QueryOptions.SkipTake(0, MaxIndexFetch));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search: Examine query failed for '{Query}'", query);
            return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((Array.Empty<SearchResultItem>(), 0));
        }

        var filtered = new List<ISearchResult>();
        foreach (var result in searchResults)
        {
            if (!int.TryParse(result.Id, out var id)) continue;

            var content = umbracoContext.Content.GetById(id);
            if (content is null) continue;
            if (content.Root().Id != tenantRootId) continue;
            if (content.Id == currentPage.Id) continue;

            filtered.Add(result);
        }

        var total = filtered.Count;
        var items = new List<SearchResultItem>(take);
        foreach (var result in filtered.Skip(skip).Take(take))
        {
            var id = int.Parse(result.Id);
            var content = umbracoContext.Content.GetById(id);
            if (content is null) continue;

            items.Add(new SearchResultItem
            {
                Name = content.Name ?? string.Empty,
                Url = content.Url(_publishedUrlProvider),
                Description =
                    BuildExcerpt(result.GetValues("metaDescription").FirstOrDefault())
                    ?? BuildExcerpt(result.GetValues("teaser").FirstOrDefault())
                    ?? BuildExcerpt(result.GetValues("contentBlocks").FirstOrDefault()),
                ContentTypeAlias = content.ContentType.Alias,
            });
        }

        return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((items, total));
    }

    private static string? BuildExcerpt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = WebUtility.HtmlDecode(HtmlTagRegex.Replace(raw, " "));
        text = WhitespaceRegex.Replace(text, " ").Trim();
        if (text.Length == 0) return null;
        if (text.Length <= ExcerptMaxChars) return text;
        return text[..ExcerptMaxChars].TrimEnd() + "…";
    }
}

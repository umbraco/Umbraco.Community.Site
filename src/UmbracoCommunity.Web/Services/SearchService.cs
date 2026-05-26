using System.Net;
using System.Text.RegularExpressions;
using Examine;
using Examine.Search;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Services;

internal sealed class SearchService : ISearchService
{
    private const int ExcerptMaxChars = 200;
    private static readonly string IndexName = Umbraco.Cms.Core.Constants.UmbracoIndexes.ExternalIndexName;
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
        int take,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || take <= 0)
        {
            return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((Array.Empty<SearchResultItem>(), 0));
        }

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
            searchResults = index.Searcher
                .CreateQuery("content")
                .ManagedQuery(query)
                .Execute(QueryOptions.SkipTake(0, Math.Max(take * 3, 10)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search: Examine query failed for '{Query}'", query);
            return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((Array.Empty<SearchResultItem>(), 0));
        }

        var items = new List<SearchResultItem>(take);
        foreach (var result in searchResults)
        {
            if (items.Count >= take) break;
            if (!int.TryParse(result.Id, out var id)) continue;

            var content = umbracoContext.Content.GetById(id);
            if (content is null) continue;
            if (content.Root().Id != tenantRootId) continue;
            if (content.Id == currentPage.Id) continue;
            if (content.TemplateId <= 0) continue; // skip non-routable items

            items.Add(new SearchResultItem
            {
                Name = content.Name ?? string.Empty,
                Url = content.Url(_publishedUrlProvider),
                Description = BuildExcerpt(
                    content.Value<string>("seoDescription")
                    ?? content.Value<string>("teaser")),
                ContentTypeAlias = content.ContentType.Alias,
            });
        }

        return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((items, (int)searchResults.TotalItemCount));
    }

    private static string? BuildExcerpt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = WebUtility.HtmlDecode(HtmlTagRegex.Replace(raw, " "));
        text = WhitespaceRegex.Replace(text, " ").Trim();
        if (text.Length <= ExcerptMaxChars) return text;
        return text[..ExcerptMaxChars].TrimEnd() + "…";
    }
}

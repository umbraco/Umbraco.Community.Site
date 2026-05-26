using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Abstract.Services;

/// <summary>
/// Runs a free-text search against the current tenant's content tree, backed by
/// Umbraco.AI.Search. Returns an empty result set if the index is unconfigured
/// or unreachable.
/// </summary>
public interface ISearchService
{
    Task<(IReadOnlyList<SearchResultItem> Results, int Total)> SearchAsync(
        IPublishedContent currentPage,
        string query,
        int take,
        CancellationToken ct);
}

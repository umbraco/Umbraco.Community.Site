using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Abstract.Services;

/// <summary>
/// Suggests pages on the current site that match free text by meaning rather than exact words,
/// backed by Umbraco.AI.Search semantic vector search.
/// </summary>
public interface IContentSuggestionService
{
    /// <summary>
    /// Returns up to <paramref name="max"/> routable, searchable pages from the current tenant's
    /// content tree that match <paramref name="query"/>, excluding <paramref name="currentPage"/>.
    /// Returns an empty list if the search index is unconfigured, unreachable or has no matches.
    /// </summary>
    Task<IReadOnlyList<IPublishedContent>> GetSuggestionsAsync(
        IPublishedContent currentPage,
        string query,
        int max,
        CancellationToken ct);
}

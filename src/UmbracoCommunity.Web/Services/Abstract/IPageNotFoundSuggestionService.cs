using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Abstract.Services;

/// <summary>
/// Resolves "you might have meant…" page suggestions for the PageNotFound view,
/// backed by Umbraco.AI.Search semantic vector search.
/// </summary>
public interface IPageNotFoundSuggestionService
{
    /// <summary>
    /// Returns up to <paramref name="max"/> content pages that semantically match the
    /// requested URL (and optionally the referring page), filtered to the current
    /// tenant's content tree. Returns an empty list if the search index is
    /// unconfigured, unreachable, or returns no usable matches.
    /// </summary>
    Task<IReadOnlyList<IPublishedContent>> GetSuggestionsAsync(
        IPublishedContent currentPage,
        string requestedPath,
        string? referrerUrl,
        int max,
        CancellationToken ct);
}

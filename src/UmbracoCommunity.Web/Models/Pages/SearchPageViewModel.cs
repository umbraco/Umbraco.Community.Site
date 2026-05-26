using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

public class SearchPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<SearchResultItem> Results { get; set; } = Array.Empty<SearchResultItem>();

    public int TotalResults { get; set; }

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
}

public sealed class SearchResultItem
{
    public required string Name { get; init; }

    public required string Url { get; init; }

    public string? Description { get; init; }

    public string? ContentTypeAlias { get; init; }
}

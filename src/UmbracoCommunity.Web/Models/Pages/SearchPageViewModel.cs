using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages;

public class SearchPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<SearchResultItem> Results { get; set; } = Array.Empty<SearchResultItem>();

    public int TotalResults { get; set; }

    public int PageSize { get; set; } = 10;

    public int CurrentPage { get; set; } = 1;

    public string BasePath { get; set; } = "/";

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalResults / (double)PageSize) : 0;

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public string GetPageUrl(int page)
        => $"{BasePath}?q={Uri.EscapeDataString(Query)}&page={page}";
}

public sealed class SearchResultItem
{
    public required string Name { get; init; }

    public required string Url { get; init; }

    public string? Description { get; init; }

    public string? ContentTypeAlias { get; init; }
}

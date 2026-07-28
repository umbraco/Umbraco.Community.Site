namespace UmbracoCommunity.Web.Services.Documentation.Search;

public interface IDocumentationSearchService
{
    IReadOnlyList<DocumentationSearchHit> Search(string query, int maxResults);
}

public sealed record DocumentationSearchHit(
    string Title,
    string Section,
    string ArticlePath,
    string? Excerpt);

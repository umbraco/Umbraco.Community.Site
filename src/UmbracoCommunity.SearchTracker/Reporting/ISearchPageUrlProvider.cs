namespace UmbracoCommunity.SearchTracker.Reporting;

/// <summary>Resolves a site's public search page, so the dashboard can link each term to its live results.</summary>
public interface ISearchPageUrlProvider
{
    /// <returns>An absolute URL, or <c>null</c> when the site has no search page.</returns>
    string? GetSearchPageUrl(Guid siteKey);
}

internal sealed class NoSearchPageUrlProvider : ISearchPageUrlProvider
{
    public string? GetSearchPageUrl(Guid siteKey) => null;
}

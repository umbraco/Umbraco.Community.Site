using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.SearchTracker.Reporting;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Services;

public class SearchPageUrlProvider : ISearchPageUrlProvider
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedUrlProvider _publishedUrlProvider;

    public SearchPageUrlProvider(IUmbracoContextFactory umbracoContextFactory, IPublishedUrlProvider publishedUrlProvider)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _publishedUrlProvider = publishedUrlProvider;
    }

    public string? GetSearchPageUrl(Guid siteKey)
    {
        using var reference = _umbracoContextFactory.EnsureUmbracoContext();
        var searchPage = reference.UmbracoContext.Content?.GetById(siteKey)?.GetSingletonPage<SearchPage>();
        return searchPage?.Url(_publishedUrlProvider, mode: UrlMode.Absolute);
    }
}

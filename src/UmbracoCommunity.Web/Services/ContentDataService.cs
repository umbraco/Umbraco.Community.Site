using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ServiceModels;

namespace UmbracoCommunity.Web.Services;

internal class ContentDataService : IContentDataService
{
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IPublishedValueFallback _publishedValueFallback;

    public ContentDataService(
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedContentQuery publishedContentQuery,
        IPublishedUrlProvider publishedUrlProvider,
        IVariationContextAccessor variationContextAccessor,
        IPublishedValueFallback publishedValueFallback)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _publishedContentQuery = publishedContentQuery;
        _publishedUrlProvider = publishedUrlProvider;
        _publishedValueFallback = publishedValueFallback;
    }
    public IReadOnlyList<SitemapElement> GetSiteMap(IPublishedContent homeNode)
    {
        var siteMapElements = new List<SitemapElement>();

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext))
        {
            return siteMapElements.AsReadOnly();
        }

        siteMapElements.Add(new SitemapElement
        {
            Url = new Uri(homeNode.Url(_publishedUrlProvider, mode: UrlMode.Absolute)),
            LastModified = homeNode.UpdateDate
        });

        AddSiteMapItems(siteMapElements, homeNode);

        return siteMapElements.AsReadOnly();
    }

    private void AddSiteMapItems(List<SitemapElement> siteMapElements, IPublishedContent currentNode)
    {
        var sitemapItemsToAdd = currentNode.Children(c => c.TemplateId.HasValue && c is IPageConfiguration pageConfig && !pageConfig.HideFromSitemap);
        foreach (IPublishedContent node in sitemapItemsToAdd ?? [])
        {
            if (node.IsVisible(_publishedValueFallback))
            {
                siteMapElements.Add(new SitemapElement
                {
                    Url = new Uri(node.Url(_publishedUrlProvider, mode: UrlMode.Absolute)),
                    LastModified = node.UpdateDate
                });
            }

            AddSiteMapItems(siteMapElements, node);
        }
    }
}

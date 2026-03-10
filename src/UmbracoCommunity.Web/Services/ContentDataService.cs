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
    
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IPublishedValueFallback _publishedValueFallback;

    public ContentDataService(
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _publishedUrlProvider = publishedUrlProvider;
        _publishedValueFallback = publishedValueFallback;
    }
    public IReadOnlyList<SitemapElement> GetSitemap(IPublishedContent homeNode)
    {
        var sitemapElements = new List<SitemapElement>();

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out _))
        {
            return sitemapElements.AsReadOnly();
        }

        sitemapElements.Add(new SitemapElement
        {
            Url = new Uri(homeNode.Url(_publishedUrlProvider, mode: UrlMode.Absolute)),
            LastModified = homeNode.UpdateDate
        });

        AddSitemapItems(sitemapElements, homeNode);

        return sitemapElements.AsReadOnly();
    }

    private void AddSitemapItems(List<SitemapElement> siteMapElements, IPublishedContent currentNode)
    {
        var sitemapItemsToAdd = currentNode.Children(c => c.TemplateId.HasValue && c is ICompositionPageConfiguration pageConfig && !pageConfig.HideFromSitemap);
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

            AddSitemapItems(siteMapElements, node);
        }
    }
}

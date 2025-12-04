using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.ServiceModels;

namespace UmbracoCommunity.Web.Abstract.Services;

public interface IContentDataService
{
    IReadOnlyList<SitemapElement> GetSitemap(IPublishedContent homeNode);
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.ServiceModels;

namespace UmbracoCommunity.Web.Controllers.Render
{
    [ResponseCache(Duration = 60)]
    public class SitemapController(
        ILogger<SitemapController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IContentDataService contentDataService) : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        private readonly IContentDataService _contentDataService = contentDataService;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor = umbracoContextAccessor;

        public override IActionResult Index()
        {
            if (_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? context) == false)
            {
                return Problem("No Umbraco Context");
            }

            var homeNode = context.PublishedRequest?.PublishedContent?.Root();

            IReadOnlyList<SitemapElement> siteMap = homeNode != null ? _contentDataService.GetSitemap(homeNode) : [];

            return new ContentResult
            {
                Content = siteMap.AsXml().ToString(),
                ContentType = "application/xml",
                StatusCode = 200
            };
        }
    }

}

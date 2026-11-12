using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Models.ServiceModels;

namespace UmbracoCommunity.Web.Controllers.Render
{
    [ResponseCache(Duration = 60)]
    public class SitemapController(
        ILogger<SitemapController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IContentDataService contentDataService,
        ProfileSitemapService profileSitemapService) : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        private readonly IContentDataService _contentDataService = contentDataService;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor = umbracoContextAccessor;

        [NonAction]
        public sealed override IActionResult Index() => throw new NotImplementedException();

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            if (_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? context) == false)
            {
                return Problem("No Umbraco Context");
            }

            var homeNode = context.PublishedRequest?.PublishedContent?.Root();

            IReadOnlyList<SitemapElement> siteMap = homeNode != null ? _contentDataService.GetSitemap(homeNode) : [];
            if (homeNode != null)
            {
                siteMap = [.. siteMap, .. await profileSitemapService.GetEntriesAsync(homeNode, cancellationToken)];
            }

            return new ContentResult
            {
                Content = siteMap.AsXml().ToString(),
                ContentType = "application/xml",
                StatusCode = 200
            };
        }
    }

}

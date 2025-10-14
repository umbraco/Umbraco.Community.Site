using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Attributes;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.Features.ReleaseOverview.Controllers
{
    public class AllReleasesController : UmbracoPageController, IVirtualPageController
    {
        private readonly IPublishedContentQuery _publishedContentQuery;
        private readonly IViewModelBuilder<AllReleasesPageViewModel> _viewModelBuilder;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public AllReleasesController(
            ILogger<UmbracoPageController> logger,
            ICompositeViewEngine compositeViewEngine,
            IPublishedContentQuery publishedContentQuery,
            IViewModelBuilder<AllReleasesPageViewModel> viewModelBuilder,
            IUmbracoContextAccessor umbracoContextAccessor)
            : base(logger, compositeViewEngine)
        {
            _publishedContentQuery = publishedContentQuery;
            _viewModelBuilder = viewModelBuilder;
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        public IPublishedContent? FindContent(ActionExecutingContext context)
        {
            // Get the root/home node to provide Umbraco context
            var rootContent = _publishedContentQuery.ContentAtRoot().FirstOrDefault();
            return rootContent;
        }

        [ApplyCommonElementsReleases]
        public IActionResult Index()
        {
            // Build the view model using the view model builder
            if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
            {
                throw new InvalidOperationException("Cannot access Umbraco context.");
            }

            var viewModel = _viewModelBuilder.Build(
                CurrentPage ?? throw new InvalidOperationException("Cannot build view model as CurrentPage is null."),
                umbracoContext);

            return View("AllReleases", viewModel);
        }
    }
}

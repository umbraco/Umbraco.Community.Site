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
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Web.Features.ReleaseOverview.Controllers
{
    public class ReleaseController : UmbracoPageController, IVirtualPageController
    {
        private readonly IPublishedContentQuery _publishedContentQuery;
        private readonly IViewModelBuilder<ReleasePageViewModel> _viewModelBuilder;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public ReleaseController(
            ILogger<UmbracoPageController> logger,
            ICompositeViewEngine compositeViewEngine,
            IPublishedContentQuery publishedContentQuery,
            IViewModelBuilder<ReleasePageViewModel> viewModelBuilder,
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
        public IActionResult Index(string org, string repo, string version, string? labelCheck)
        {
            // Build the view model using the view model builder
            if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
            {
                throw new InvalidOperationException("Cannot access Umbraco context.");
            }

            // Cast to the concrete builder to access the overload with parameters
            var viewModel = ((ReleasePageViewModelBuilder)_viewModelBuilder).Build(
                CurrentPage ?? throw new InvalidOperationException("Cannot build view model as CurrentPage is null."),
                umbracoContext,
                org,
                repo,
                version);

            // Set label check from query string
            viewModel.LabelCheck = !string.IsNullOrEmpty(labelCheck) && labelCheck.Equals("true", StringComparison.OrdinalIgnoreCase);

            return View("SingleRelease", viewModel);
        }
    }
}

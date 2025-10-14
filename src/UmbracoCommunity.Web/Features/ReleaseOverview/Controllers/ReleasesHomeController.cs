using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Attributes;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.Features.ReleaseOverview.Controllers
{
    public class ReleasesHomeController : RenderController
    {
        private readonly IViewModelBuilder<ReleasesHomePageViewModel> _viewModelBuilder;

        public ReleasesHomeController(
            ILogger<ReleasesHomeController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IViewModelBuilder<ReleasesHomePageViewModel> viewModelBuilder)
            : base(logger, compositeViewEngine, umbracoContextAccessor) => _viewModelBuilder = viewModelBuilder;

        [ApplyCommonElementsReleases]
        public override IActionResult Index()
        {
            ReleasesHomePageViewModel viewModel = _viewModelBuilder.Build(
                CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null."),
                UmbracoContext);

            return CurrentTemplate(viewModel);
        }
    }
}

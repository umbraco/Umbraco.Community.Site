using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Attributes;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.Controllers
{
    public class HomeController : RenderController
    {
        private readonly IViewModelBuilder<HomePageViewModel> _viewModelBuilder;

        public HomeController(
            ILogger<HomeController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IViewModelBuilder<HomePageViewModel> viewModelBuilder)
            : base(logger, compositeViewEngine, umbracoContextAccessor) => _viewModelBuilder = viewModelBuilder;

        [NonAction]
        public sealed override IActionResult Index() => throw new NotImplementedException();

        [ApplyCommonElements]
        public IActionResult Index(CancellationToken cancellationToken)
        {
            HomePageViewModel viewModel = _viewModelBuilder.Build(
                CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null."),
                UmbracoContext);
            return CurrentTemplate(viewModel);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.Controllers.Render
{
    public class ContentPageController : RenderController
    {
        private readonly IViewModelBuilder<ContentPageViewModel> _viewModelBuilder;

        public ContentPageController(
            ILogger<ContentPageController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IViewModelBuilder<ContentPageViewModel> viewModelBuilder)
            : base(logger, compositeViewEngine, umbracoContextAccessor) => _viewModelBuilder = viewModelBuilder;

        [NonAction]
        public sealed override IActionResult Index() => throw new NotImplementedException();

        public IActionResult Index(CancellationToken cancellationToken)
        {
            ContentPageViewModel viewModel = _viewModelBuilder.Build(
                CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null."),
                UmbracoContext);
            return CurrentTemplate(viewModel);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Models.Pages.Documentation;
using UmbracoCommunity.Web.Routing;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Web.Controllers.Render;

public class DocumentationController : RenderController
{
    private readonly DocumentationPageViewModelBuilder _viewModelBuilder;

    public DocumentationController(
        ILogger<DocumentationController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        DocumentationPageViewModelBuilder viewModelBuilder)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _viewModelBuilder = viewModelBuilder;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    public IActionResult Index(CancellationToken cancellationToken)
    {
        var currentPage = CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null.");

        var segments = (HttpContext.Items[DocumentationContentFinder.PathSegmentsItemKey] as string[]) ?? [];

        var viewModel = _viewModelBuilder.Build(currentPage, UmbracoContext, segments);
        if (viewModel is null)
        {
            return NotFound();
        }

        return CurrentTemplate(viewModel);
    }
}

using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Services;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.ViewComponents;

public class FooterViewComponent : ViewComponent
{
    private readonly IViewModelBuilder<FooterViewModel> _footerViewModelBuilder;
    private readonly ContentContextService _currentPageService;

    public FooterViewComponent(
        IViewModelBuilder<FooterViewModel> footerViewModelBuilder,
        ContentContextService currentPageService)
    {
        _footerViewModelBuilder = footerViewModelBuilder;
        _currentPageService = currentPageService;
    }

    public IViewComponentResult Invoke()
    {
        var currentPage = _currentPageService.CurrentPage;
        var umbracoContext = _currentPageService.UmbracoContext;

        if (currentPage is null || umbracoContext is null)
        {
            return View("Footer", new FooterViewModel());
        }

        var viewModel = _footerViewModelBuilder.Build(currentPage, umbracoContext);
        return View("Footer", viewModel);
    }
}

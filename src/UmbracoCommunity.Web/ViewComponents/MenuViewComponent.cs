using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Services;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.ViewComponents;

public class MenuViewComponent : ViewComponent
{
    private readonly IViewModelBuilder<MenuViewModel> _menuViewModelBuilder;
    private readonly ContentContextService _currentPageService;

    public MenuViewComponent(
        IViewModelBuilder<MenuViewModel> menuViewModelBuilder,
        ContentContextService currentPageService)
    {
        _menuViewModelBuilder = menuViewModelBuilder;
        _currentPageService = currentPageService;
    }

    public IViewComponentResult Invoke()
    {
        var currentPage = _currentPageService.CurrentPage;
        var umbracoContext = _currentPageService.UmbracoContext;

        if (currentPage is null || umbracoContext is null)
        {
            return View("Menu", new MenuViewModel());
        }

        var viewModel = _menuViewModelBuilder.Build(currentPage, umbracoContext);
        return View("Menu", viewModel);
    }
}

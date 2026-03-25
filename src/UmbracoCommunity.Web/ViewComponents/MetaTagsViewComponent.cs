using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Services;

namespace UmbracoCommunity.Web.ViewComponents;

public class MetaTagsViewComponent : ViewComponent
{
    private readonly ISeoDataService _seoDataService;
    private readonly ContentContextService _currentPageService;

    public MetaTagsViewComponent(
        ISeoDataService seoDataService,
        ContentContextService currentPageService)
    {
        _seoDataService = seoDataService;
        _currentPageService = currentPageService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var currentPage = _currentPageService.CurrentPage;

        if (currentPage is null)
        {
            return View("MetaTags", new MetaTagsViewModel());
        }

        var viewModel = await _seoDataService.BuildAsync(currentPage);
        return View("MetaTags", viewModel);
    }
}

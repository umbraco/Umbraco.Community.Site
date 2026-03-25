using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Services;

namespace UmbracoCommunity.Web.ViewComponents;

public class FaviconViewComponent : ViewComponent
{
    private readonly ContentContextService _currentPageService;

    public FaviconViewComponent(ContentContextService currentPageService)
    {
        _currentPageService = currentPageService;
    }

    public IViewComponentResult Invoke()
    {
        var currentPage = _currentPageService.CurrentPage;
        MediaWithCrops? favicon = currentPage?.GetSiteSettings()?.Favicon;
        return View("Favicon", favicon);
    }
}

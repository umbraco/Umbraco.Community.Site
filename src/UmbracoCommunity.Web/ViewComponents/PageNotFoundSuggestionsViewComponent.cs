using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Abstract.Services;

namespace UmbracoCommunity.Web.ViewComponents;

public class PageNotFoundSuggestionsViewComponent : ViewComponent
{
    private const int MaxSuggestions = 4;

    private readonly IPageNotFoundSuggestionService _service;

    public PageNotFoundSuggestionsViewComponent(IPageNotFoundSuggestionService service)
    {
        _service = service;
    }

    public async Task<IViewComponentResult> InvokeAsync(IPublishedContent currentPage)
    {
        var request = HttpContext.Request;
        var path = request.Path.Value ?? string.Empty;
        var referrer = request.Headers.Referer.ToString();
        if (string.IsNullOrEmpty(referrer)) referrer = null;

        var suggestions = await _service.GetSuggestionsAsync(
            currentPage,
            path,
            referrer,
            MaxSuggestions,
            HttpContext.RequestAborted);

        return View(suggestions);
    }
}

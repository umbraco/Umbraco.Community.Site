using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Controllers.Render
{
    public class SearchPageController : RenderController
    {
        private const int PageSize = 10;

        private readonly ISearchService _searchService;

        public SearchPageController(
            ILogger<SearchPageController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            ISearchService searchService)
            : base(logger, compositeViewEngine, umbracoContextAccessor) => _searchService = searchService;

        [NonAction]
        public sealed override IActionResult Index() => throw new NotImplementedException();

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var currentPage = CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null.");
            var query = (Request.Query["q"].ToString() ?? string.Empty).Trim();
            var page = ParsePage(Request.Query["page"].ToString());

            var viewModel = new SearchPageViewModel(currentPage)
            {
                Query = query,
                PageSize = PageSize,
                CurrentPage = page,
                BasePath = Request.Path.Value ?? "/",
            };

            if (viewModel.HasQuery)
            {
                var skip = (page - 1) * PageSize;
                var (results, total) = await _searchService.SearchAsync(currentPage, query, skip, PageSize, cancellationToken);
                viewModel.Results = results;
                viewModel.TotalResults = total;

                // Clamp current page if the request asked for one past the end (e.g. user changed query).
                if (viewModel.TotalPages > 0 && page > viewModel.TotalPages)
                {
                    viewModel.CurrentPage = viewModel.TotalPages;
                }
            }

            return CurrentTemplate(viewModel);
        }

        private static int ParsePage(string? raw)
            => int.TryParse(raw, out var p) && p > 0 ? p : 1;
    }
}

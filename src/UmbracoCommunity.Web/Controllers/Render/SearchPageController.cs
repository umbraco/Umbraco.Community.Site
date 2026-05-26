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
        private const int MaxResults = 50;

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

            var viewModel = new SearchPageViewModel(currentPage) { Query = query };

            if (viewModel.HasQuery)
            {
                var (results, total) = await _searchService.SearchAsync(currentPage, query, MaxResults, cancellationToken);
                viewModel.Results = results;
                viewModel.TotalResults = total;
            }

            return CurrentTemplate(viewModel);
        }
    }
}

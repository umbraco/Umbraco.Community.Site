using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoCommunity.SearchTracker.Recording;
using UmbracoCommunity.Web.Abstract.Services;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Controllers.Render
{
    public class SearchPageController : RenderController
    {
        private const int PageSize = 10;
        private const int MaxSuggestions = 4;
        private const int MaxResultsForRelated = 3;
        private const int MaxRelated = 3;

        private readonly ISearchService _searchService;
        private readonly ISearchTermRecorder _searchTermRecorder;
        private readonly IContentSuggestionService _contentSuggestions;

        public SearchPageController(
            ILogger<SearchPageController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            ISearchService searchService,
            ISearchTermRecorder searchTermRecorder,
            IContentSuggestionService contentSuggestions)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _searchService = searchService;
            _searchTermRecorder = searchTermRecorder;
            _contentSuggestions = contentSuggestions;
        }

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
                var (results, total) = await _searchService.SearchAsync(currentPage, query, (page - 1) * PageSize, PageSize, cancellationToken);
                viewModel.TotalResults = total;

                if (viewModel.TotalPages > 0 && page > viewModel.TotalPages)
                {
                    viewModel.CurrentPage = viewModel.TotalPages;
                    (results, total) = await _searchService.SearchAsync(currentPage, query, (viewModel.CurrentPage - 1) * PageSize, PageSize, cancellationToken);
                }

                viewModel.Results = results;

                if (page == 1)
                {
                    _searchTermRecorder.Record(currentPage.Root().Key, query, total, HttpContext);
                }

                if (viewModel.CurrentPage == 1)
                {
                    await AddSuggestionsAsync(viewModel, currentPage, cancellationToken);
                }
            }

            return CurrentTemplate(viewModel);
        }

        private async Task AddSuggestionsAsync(SearchPageViewModel viewModel, IPublishedContent currentPage, CancellationToken cancellationToken)
        {
            if (viewModel.TotalResults == 0)
            {
                viewModel.Suggestions = await _contentSuggestions.GetSuggestionsAsync(currentPage, viewModel.Query, MaxSuggestions, cancellationToken);
            }
            else if (viewModel.TotalResults <= MaxResultsForRelated)
            {
                var suggestions = await _contentSuggestions.GetSuggestionsAsync(currentPage, viewModel.Query, MaxRelated + viewModel.Results.Count, cancellationToken);
                viewModel.RelatedResults = SelectRelated(suggestions, viewModel.Results, MaxRelated);
            }
        }

        internal static IReadOnlyList<IPublishedContent> SelectRelated(
            IEnumerable<IPublishedContent> suggestions,
            IEnumerable<SearchResultItem> results,
            int max)
        {
            var shown = results.Select(r => r.ContentKey).OfType<Guid>().ToHashSet();
            return suggestions.Where(p => !shown.Contains(p.Key)).Take(max).ToList();
        }

        private static int ParsePage(string? raw)
            => int.TryParse(raw, out var p) && p > 0 ? p : 1;
    }
}

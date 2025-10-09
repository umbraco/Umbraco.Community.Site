using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.Attributes
{
    public class ApplyCommonElementsReleasesAttribute : TypeFilterAttribute
    {
        public ApplyCommonElementsReleasesAttribute() : base(typeof(ApplyNavigationFilter))
        {
        }

        private class ApplyNavigationFilter : ApplyFilterBase, IResultFilter
        {
            private readonly IViewModelBuilder<MenuViewModel> _menuReleasesViewModelBuilder;
            private readonly ICspNonceService _nonceService;

            public ApplyNavigationFilter(
                IUmbracoContextAccessor umbracoContextAccessor,
                IViewModelBuilder<MenuReleasesViewModel> menuReleasesViewModelBuilder,
                ICspNonceService nonceService)
                : base(umbracoContextAccessor)
            {
                _menuReleasesViewModelBuilder = menuReleasesViewModelBuilder;
                _nonceService = nonceService;
            }

            public void OnResultExecuting(ResultExecutingContext context)
            {
                if (!TryGetPageAndUmbracoContext(context, out PageViewModelBase? viewModel, out IUmbracoContext? umbracoContext))
                {
                    return;
                }

                if (!TryGetPublishedContent(umbracoContext, out IPublishedContent? publishedContent))
                {
                    return;
                }

                viewModel.Menu = _menuReleasesViewModelBuilder.Build(publishedContent, umbracoContext);
            }

            public void OnResultExecuted(ResultExecutedContext context)
            {
            }
        }
    }
}

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
    public class ApplyCommonElementsAttribute : TypeFilterAttribute
    {
        public ApplyCommonElementsAttribute() : base(typeof(ApplyNavigationFilter))
        {
        }

        private class ApplyNavigationFilter : ApplyFilterBase, IResultFilter
        {
            private readonly IViewModelBuilder<MenuViewModel> _menuViewModelBuilder;
            private readonly ICspNonceService _nonceService;

            public ApplyNavigationFilter(
                IUmbracoContextAccessor umbracoContextAccessor,
                IViewModelBuilder<MenuViewModel> menuViewModelBuilder,
                ICspNonceService nonceService)
                : base(umbracoContextAccessor)
            {
                _menuViewModelBuilder = menuViewModelBuilder;
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

                viewModel.Menu = _menuViewModelBuilder.Build(publishedContent, umbracoContext);
            }

            public void OnResultExecuted(ResultExecutedContext context)
            {
            }
        }
    }
}

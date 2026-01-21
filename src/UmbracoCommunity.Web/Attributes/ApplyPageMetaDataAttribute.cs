using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.ViewModelBuilders;

namespace UmbracoCommunity.Web.Attributes
{
    public class ApplyPageMetaDataAttribute : TypeFilterAttribute
    {
        public ApplyPageMetaDataAttribute() : base(typeof(ApplyPageMetaDataFilter))
        {
        }

        private class ApplyPageMetaDataFilter : ApplyFilterBase, IAsyncResultFilter
        {
            private readonly IPageViewModelDecorator<Seo> _viewModelDecorator;

            public ApplyPageMetaDataFilter(IUmbracoContextAccessor umbracoContextAccessor,
                IPageViewModelDecorator<Seo> viewModelDecorator)
                : base(umbracoContextAccessor) => _viewModelDecorator = viewModelDecorator;

            public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
            {
                if (TryGetPageAndUmbracoContext(context, out PageViewModelBase? viewModel, 
                        out IUmbracoContext? umbracoContext)
                    && TryGetPublishedContent(umbracoContext, out IPublishedContent? publishedContent))
                {
                    await _viewModelDecorator.DecorateAsync(viewModel, publishedContent);
                }

                await next();
            }
        }
    }
}
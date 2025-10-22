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

        private class ApplyPageMetaDataFilter : ApplyFilterBase, IResultFilter
        {
            private readonly IPageViewModelDecorator<Seo> _viewModelDecorator;

            public ApplyPageMetaDataFilter(IUmbracoContextAccessor umbracoContextAccessor, IPageViewModelDecorator<Seo> viewModelDecorator)
                : base(umbracoContextAccessor) => _viewModelDecorator = viewModelDecorator;

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

                _viewModelDecorator.Decorate(viewModel, publishedContent);
            }

            public void OnResultExecuted(ResultExecutedContext context)
            {
            }
        }
    }
}

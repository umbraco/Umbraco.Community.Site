using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics.CodeAnalysis;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.Attributes
{
    public abstract class ApplyFilterBase
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        protected ApplyFilterBase(IUmbracoContextAccessor umbracoContextAccessor) => _umbracoContextAccessor = umbracoContextAccessor;

        protected bool TryGetPageAndUmbracoContext(ResultExecutingContext context, [NotNullWhen(true)] out PageViewModelBase? viewModel, [NotNullWhen(true)] out IUmbracoContext? umbracoContext)
        {
            if (context.Result is ViewResult viewResult &&
                viewResult.Model is PageViewModelBase pageViewModel &&
                _umbracoContextAccessor.TryGetUmbracoContext(out umbracoContext))
            {
                viewModel = pageViewModel;
                return true;
            }

            umbracoContext = null;
            viewModel = null;
            return false;
        }

        protected static bool TryGetPublishedContent(IUmbracoContext umbracoContext, [NotNullWhen(true)] out IPublishedContent? publishedContent)
        {
            publishedContent = umbracoContext.PublishedRequest?.PublishedContent;
            return publishedContent is not null;
        }
    }
}

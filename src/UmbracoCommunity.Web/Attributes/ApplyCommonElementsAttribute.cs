using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Core.Web;

namespace UmbracoCommunity.Web.Attributes
{
    public class ApplyCommonElementsAttribute : TypeFilterAttribute
    {
        public ApplyCommonElementsAttribute() : base(typeof(ApplyNavigationFilter))
        {
        }

        private class ApplyNavigationFilter : ApplyFilterBase, IResultFilter
        {

            //private readonly ICspNonceService _nonceService;

            public ApplyNavigationFilter(
                IUmbracoContextAccessor umbracoContextAccessor)
                : base(umbracoContextAccessor)
            {
                //_nonceService = nonceService;
            }

            public void OnResultExecuting(ResultExecutingContext context)
            {
            }

            public void OnResultExecuted(ResultExecutedContext context)
            {
            }
        }
    }
}

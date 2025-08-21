using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using static Umbraco.Cms.Core.Constants;

namespace UmbracoCommunity.Web.Middleware
{
    public class DisableCspMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        private const string CspHeaderName = "Content-Security-Policy";

        private readonly string[] _disabledCspBlocks = [];
        private readonly string[] _disabledCspProperties = [PropertyEditors.Aliases.BlockList];

        public DisableCspMiddleware(IUmbracoContextAccessor umbracoContextAccessor, RequestDelegate next)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _next = next;
        }

        public async Task InvokeAsync(Microsoft.AspNetCore.Http.HttpContext context)
        {
            if (_umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext) && umbracoContext?.PublishedRequest?.PublishedContent is not null)
            {
                if (ContainsCspHeader(context.Response) && ContainsInsecureBlocks(umbracoContext.PublishedRequest.PublishedContent))
                {
                    context.Response.Headers.Remove(CspHeaderName);
                }
            }

            await _next(context);
        }

        private bool ContainsInsecureBlocks(IPublishedContent model)
        {
            IEnumerable<IPublishedProperty> properties = model.Properties
                .Where(x => _disabledCspProperties.Contains(x.PropertyType.DataType.EditorAlias, StringComparer.InvariantCultureIgnoreCase));

            return properties?.Any(p => model.Value<BlockListModel>(p.Alias)?
                .Any(x => _disabledCspBlocks.Contains(x.Content.ContentType.Alias, StringComparer.InvariantCultureIgnoreCase)) ?? false) ?? false;
        }

        private bool ContainsCspHeader(HttpResponse response) => response.Headers.Any(h => h.Key.Equals(CspHeaderName, StringComparison.OrdinalIgnoreCase));
    }
}

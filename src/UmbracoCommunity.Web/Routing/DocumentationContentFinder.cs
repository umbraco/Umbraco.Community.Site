using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Services.Documentation;

namespace UmbracoCommunity.Web.Routing;

/// <summary>
/// Intercepts requests under the per-tenant Documentation node and binds them to that node
/// while stashing the trailing path segments for the render controller to resolve against the markdown index.
/// </summary>
public class DocumentationContentFinder : IContentFinder
{
    public const string PathSegmentsItemKey = "Documentation:PathSegments";

    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DocumentationContentFinder(
        IUmbracoContextAccessor umbracoContextAccessor,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory serviceScopeFactory)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _httpContextAccessor = httpContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
        {
            return Task.FromResult(false);
        }

        var rootContentId = request.Domain?.ContentId;
        List<Models.PublishedModels.Documentation> documentationNodes;

        if (rootContentId is not null
            && umbracoContext.Content?.GetById(rootContentId.Value) is { } tenantRoot)
        {
            documentationNodes = tenantRoot.DescendantsOrSelf<Models.PublishedModels.Documentation>().ToList();
        }
        else
        {
            // Local/single-tenant setups without a domain binding — walk all root nodes via
            // a scoped IPublishedContentQuery (content finders are singletons; the query is scoped).
            using var scope = _serviceScopeFactory.CreateScope();
            var contentQuery = scope.ServiceProvider.GetRequiredService<IPublishedContentQuery>();
            documentationNodes = contentQuery.ContentAtRoot()
                .SelectMany(r => r.DescendantsOrSelf<Models.PublishedModels.Documentation>())
                .ToList();
        }

        var requestPath = request.Uri.AbsolutePath;

        foreach (var docNode in documentationNodes)
        {
            var docUrl = docNode.Url(mode: UrlMode.Relative);
            if (string.IsNullOrEmpty(docUrl) || docUrl == "#")
            {
                continue;
            }

            var normalised = docUrl.TrimEnd('/');
            if (!requestPath.StartsWith(normalised + "/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(requestPath.TrimEnd('/'), normalised, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = requestPath.Length > normalised.Length
                ? requestPath[normalised.Length..]
                : string.Empty;

            var segments = remainder
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            request.SetPublishedContent(docNode);

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is not null)
            {
                httpContext.Items[PathSegmentsItemKey] = segments;
            }

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

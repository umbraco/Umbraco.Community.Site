using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.NotFoundTracker.Routing;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.Routing;

/// <summary>
/// Tenant-aware "PageNotFound" resolver. Walks the current request's domain root for a
/// PageNotFound node and falls back to the first one found across all root content nodes.
/// Implements <see cref="INotFoundPageResolver"/> for the Umbraco.Community.NotFoundTracker package.
/// </summary>
public class CommunitySitePageNotFoundResolver : INotFoundPageResolver
{
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CommunitySitePageNotFoundResolver(
        IUmbracoContextAccessor umbracoContextAccessor,
        IServiceScopeFactory serviceScopeFactory)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task<IPublishedContent?> ResolveAsync(IPublishedRequestBuilder request)
    {
        Models.PublishedModels.PageNotFound? notFoundPage = null;

        var rootContentId = request.Domain?.ContentId;

        if (rootContentId is not null
            && _umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
        {
            var rootNode = umbracoContext.Content?.GetById(rootContentId.Value);
            notFoundPage = rootNode?
                .DescendantsOrSelf<Models.PublishedModels.PageNotFound>()
                .FirstOrDefault();
        }

        if (notFoundPage is null)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var contentQuery = scope.ServiceProvider.GetRequiredService<IPublishedContentQuery>();
            notFoundPage = contentQuery.ContentAtRoot()
                .SelectMany(r => r.DescendantsOrSelf<Models.PublishedModels.PageNotFound>())
                .FirstOrDefault();
        }

        return Task.FromResult<IPublishedContent?>(notFoundPage);
    }
}

using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.Routing;

public class PageNotFoundContentFinder : IContentLastChanceFinder
{
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PageNotFoundContentFinder(
        IUmbracoContextAccessor umbracoContextAccessor,
        IServiceScopeFactory serviceScopeFactory)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder request)
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

        // Fallback: find the first PageNotFound node across all root nodes
        if (notFoundPage is null)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var contentQuery = scope.ServiceProvider.GetRequiredService<IPublishedContentQuery>();
            notFoundPage = contentQuery.ContentAtRoot()
                .SelectMany(r => r.DescendantsOrSelf<Models.PublishedModels.PageNotFound>())
                .FirstOrDefault();
        }

        if (notFoundPage is null)
        {
            return Task.FromResult(false);
        }

        request.SetPublishedContent(notFoundPage);
        request.SetResponseStatus(404);

        return Task.FromResult(true);
    }
}

public class PageNotFoundContentFinderComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.SetContentLastChanceFinder<PageNotFoundContentFinder>();
    }
}

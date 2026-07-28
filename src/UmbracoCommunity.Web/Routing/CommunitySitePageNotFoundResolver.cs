using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<CommunitySitePageNotFoundResolver> _logger;

    public CommunitySitePageNotFoundResolver(
        IUmbracoContextAccessor umbracoContextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CommunitySitePageNotFoundResolver> logger)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public Task<IPublishedContent?> ResolveAsync(IPublishedRequestBuilder request)
    {
        Models.PublishedModels.PageNotFound? notFoundPage = null;

        var rootContentId = request.Domain?.ContentId;
        _logger.LogDebug(
            "PageNotFound resolver: domain='{Domain}' rootContentId={RootId} requestUri='{Uri}'",
            request.Domain?.Name, rootContentId, request.Uri);

        if (rootContentId is not null
            && _umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
        {
            var rootNode = umbracoContext.Content?.GetById(rootContentId.Value);
            notFoundPage = rootNode?
                .DescendantsOrSelf<Models.PublishedModels.PageNotFound>()
                .FirstOrDefault();
            _logger.LogDebug(
                "PageNotFound resolver: domain branch — rootNode={RootNode}, found={FoundId}",
                rootNode?.Id, notFoundPage?.Id);
        }

        if (notFoundPage is null)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var contentQuery = scope.ServiceProvider.GetRequiredService<IPublishedContentQuery>();
            var roots = contentQuery.ContentAtRoot().ToList();
            notFoundPage = roots
                .SelectMany(r => r.DescendantsOrSelf<Models.PublishedModels.PageNotFound>())
                .FirstOrDefault();
            _logger.LogDebug(
                "PageNotFound resolver: fallback branch — roots=[{Roots}], found={FoundId}",
                string.Join(",", roots.Select(r => $"{r.Id}:{r.ContentType.Alias}")),
                notFoundPage?.Id);
        }

        if (notFoundPage is null)
        {
            _logger.LogWarning(
                "PageNotFound resolver: NO PageNotFound node found — Umbraco will render NoNodes fallback.");
        }

        return Task.FromResult<IPublishedContent?>(notFoundPage);
    }
}

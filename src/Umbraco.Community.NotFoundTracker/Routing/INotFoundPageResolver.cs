using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;

namespace Umbraco.Community.NotFoundTracker.Routing;

/// <summary>
/// Host plug-in: returns the content node to render for a 404 (typically a tenant-aware
/// "PageNotFound" page). The package's <see cref="NotFoundTrackingContentFinder"/> calls this
/// after recording the hit. Return <c>null</c> to let Umbraco serve a default 404.
/// </summary>
public interface INotFoundPageResolver
{
    Task<IPublishedContent?> ResolveAsync(IPublishedRequestBuilder request);
}

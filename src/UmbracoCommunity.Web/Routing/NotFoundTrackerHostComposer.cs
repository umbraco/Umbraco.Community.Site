using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.NotFoundTracker;
using Umbraco.Community.NotFoundTracker.Routing;

namespace UmbracoCommunity.Web.Routing;

/// <summary>
/// Host composer: registers the Umbraco.Community.NotFoundTracker package and plugs in
/// the tenant-aware page resolver. Replaces the previous PageNotFoundContentFinderComposer.
/// </summary>
public class NotFoundTrackerHostComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotFoundTracker();
        builder.Services.AddSingleton<INotFoundPageResolver, CommunitySitePageNotFoundResolver>();
    }
}

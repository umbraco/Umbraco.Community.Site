using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Routing;

namespace UmbracoCommunity.Web.ViewModelBuilders.Components;

internal abstract class NavigationViewModelBuilderBase : ViewModelBuilderBase
{
    protected NavigationViewModelBuilderBase(
        IPublishedContentQuery publishedContentQuery,
        IPublishedUrlProvider publishedUrlProvider,
        AppCaches appCaches)
    {
        PublishedContentQuery = publishedContentQuery;
        PublishedUrlProvider = publishedUrlProvider;
        AppCaches = appCaches;
    }

    protected IPublishedContentQuery PublishedContentQuery { get; }

    protected IPublishedUrlProvider PublishedUrlProvider { get; }

    protected AppCaches AppCaches { get; }

    //protected NavigationSettings GetNavigationSettings()
    //{
    //    IPublishedContent? settingsNode = PublishedContentQuery.ContentAtRoot().FirstOrDefault(x => x.ContentType.Alias == Settings.ModelTypeAlias)
    //        ?? throw new InvalidOperationException("Could not find the expected settings node at the site root.");

    //    if (settingsNode.Children.FirstOrDefault(x => x.ContentType.Alias == NavigationSettings.ModelTypeAlias) is not NavigationSettings navigationSettings)
    //    {
    //        throw new InvalidOperationException("Could not find the expected navigation settings node as a child of the settings node.");
    //    }

    //    return navigationSettings;
    //}
}

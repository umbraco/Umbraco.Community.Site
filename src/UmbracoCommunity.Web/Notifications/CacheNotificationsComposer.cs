using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoCommunity.Web.Notifications;

/// <summary>
/// Registers notification handlers for cache invalidation.
/// </summary>
public class CacheNotificationsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<ContentCacheRefresherNotification, BlogContentCacheInvalidationHandler>();
    }
}

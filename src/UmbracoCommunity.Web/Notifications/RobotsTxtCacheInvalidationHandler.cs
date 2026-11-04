using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoCommunity.Web.Controllers;

namespace UmbracoCommunity.Web.Notifications;

// Refresher payloads carry ids, not content types, so clear on any content refresh.
public class RobotsTxtCacheInvalidationHandler : INotificationHandler<ContentCacheRefresherNotification>
{
    private readonly AppCaches _appCaches;

    public RobotsTxtCacheInvalidationHandler(AppCaches appCaches) => _appCaches = appCaches;

    public void Handle(ContentCacheRefresherNotification notification)
        => _appCaches.RuntimeCache.ClearByKey(RobotsController.CustomRobotsTxtCacheKeyPrefix);
}

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoCommunity.Web.Notifications;

public class ContentValidationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationHandler<ContentSavingNotification, HeroVideoSizeValidationHandler>();
        builder.AddNotificationHandler<ContentPublishingNotification, HeroVideoSizeValidationHandler>();
    }
}

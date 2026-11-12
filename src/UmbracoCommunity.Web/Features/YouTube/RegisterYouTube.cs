using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.Features.YouTube;

public sealed class RegisterYouTube : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddHttpClient(YouTubePosterService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0");
        });
        builder.Services.AddScoped<IYouTubePosterService, YouTubePosterService>();

        builder.DeployValueConnectors().Add<YouTubeVideoValueConnector>();
        builder.AddNotificationHandler<ContentSavingNotification, LegacyVideoBlockGuard>();
    }
}

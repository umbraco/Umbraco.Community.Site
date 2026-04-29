using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using UmbracoCommunity.Web.Features.Feeds.Calendar;

namespace UmbracoCommunity.Web.Features.Feeds.Configuration;

public sealed class RegisterFeeds : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddHttpClient<CalendarFeedHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.Services.AddSingleton<ICalendarFeedService, CalendarFeedService>();
    }
}

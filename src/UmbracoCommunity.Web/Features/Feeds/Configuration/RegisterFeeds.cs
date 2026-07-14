using Examine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using UmbracoCommunity.Web.Features.Feeds.Calendar;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

namespace UmbracoCommunity.Web.Features.Feeds.Configuration;

public sealed class RegisterFeeds : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<CalendarFeedOptions>(
            builder.Config.GetSection(CalendarFeedOptions.SectionName));

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

        // --- Community blog posts (Umbraco Sphere API) ---
        builder.Services.Configure<CommunityBlogsOptions>(
            builder.Config.GetSection(CommunityBlogsOptions.SectionName));

        var communityBlogsOptions =
            builder.Config.GetSection(CommunityBlogsOptions.SectionName).Get<CommunityBlogsOptions>()
            ?? new CommunityBlogsOptions();

        builder.Services.AddHttpClient<SphereHttpClient>(client =>
        {
            client.BaseAddress = new Uri(communityBlogsOptions.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, communityBlogsOptions.RequestTimeoutSeconds));
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.Services.AddSingleton<SphereApiClient>();
        builder.Services.AddSingleton<CommunityBlogsAggregator>();

        builder.Services.AddHttpClient<CommunityBlogsImageHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.Services.AddSingleton<CommunityBlogsImageDownloader>();
        builder.Services.AddExamineLuceneIndex(CommunityBlogsSearchIndexer.IndexName);
        builder.Services.AddSingleton<ICommunityBlogsIndexer, CommunityBlogsSearchIndexer>();
        builder.Services.AddSingleton<ICommunityBlogsService, CommunityBlogsService>();
    }
}

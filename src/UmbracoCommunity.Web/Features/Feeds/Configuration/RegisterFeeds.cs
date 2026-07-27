using System.Net;
using System.Net.Sockets;
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

        // --- Community blog posts (aggregated from an external content platform) ---
        builder.Services.Configure<CommunityBlogsOptions>(
            builder.Config.GetSection(CommunityBlogsOptions.SectionName));

        var communityBlogsOptions =
            builder.Config.GetSection(CommunityBlogsOptions.SectionName).Get<CommunityBlogsOptions>()
            ?? new CommunityBlogsOptions();

        builder.Services.AddHttpClient<CommunityBlogsHttpClient>(client =>
        {
            // ApiBaseUrl has no default (see CommunityBlogsOptions) — an environment without it
            // configured just leaves BaseAddress unset; CommunityBlogsAggregator already no-ops
            // when ApiKey is missing, so no request is ever attempted against it.
            if (!string.IsNullOrWhiteSpace(communityBlogsOptions.ApiBaseUrl))
            {
                client.BaseAddress = new Uri(communityBlogsOptions.ApiBaseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, communityBlogsOptions.RequestTimeoutSeconds));
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false });

        builder.Services.AddSingleton<CommunityBlogsApiClient>();
        builder.Services.AddSingleton<CommunityBlogsAggregator>();

        builder.Services.AddHttpClient<CommunityBlogsImageHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        });

        builder.Services.AddSingleton<CommunityBlogsImageDownloader>();

        // Dedicated client for FeedSubmissionImageProxyService, which fetches a caller-supplied URL (a
        // feed's own cover/avatar image). Redirects are disabled and the connection is pinned to a
        // pre-validated public IP via ConnectCallback so a same-request redirect or a DNS-rebinding attack
        // can't smuggle the request past the SSRF check performed before this client is ever invoked.
        builder.Services.AddHttpClient<FeedSubmissionImageProxyHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var address = await PublicNetworkGuard.ResolvePublicAddressAsync(context.DnsEndPoint.Host, cancellationToken)
                    ?? throw new HttpRequestException($"'{context.DnsEndPoint.Host}' does not resolve to a permitted address.");

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        });
        builder.Services.AddSingleton<FeedSubmissionImageProxyService>();
        builder.Services.AddExamineLuceneIndex(CommunityBlogsSearchIndexer.IndexName);
        builder.Services.AddSingleton<ICommunityBlogsIndexer, CommunityBlogsSearchIndexer>();
        builder.Services.AddSingleton<ICommunityBlogsService, CommunityBlogsService>();
    }
}

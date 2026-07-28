namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>Marker type for the named HttpClient used to download community blog images. Configured in RegisterFeeds.</summary>
public sealed class CommunityBlogsImageHttpClient
{
    public HttpClient Client { get; }
    public CommunityBlogsImageHttpClient(HttpClient client) => Client = client;
}

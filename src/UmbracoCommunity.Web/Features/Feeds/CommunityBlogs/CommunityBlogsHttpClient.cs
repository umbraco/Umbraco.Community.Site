namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Marker type for the named HttpClient used by <see cref="CommunityBlogsApiClient"/>.
/// Configured in <c>RegisterFeeds</c>.
/// </summary>
public sealed class CommunityBlogsHttpClient
{
    public HttpClient Client { get; }

    public CommunityBlogsHttpClient(HttpClient client) => Client = client;
}

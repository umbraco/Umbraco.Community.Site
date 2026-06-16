namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Marker type for the named HttpClient used by <see cref="SphereApiClient"/>.
/// Configured in <c>RegisterFeeds</c>.
/// </summary>
public sealed class SphereHttpClient
{
    public HttpClient Client { get; }

    public SphereHttpClient(HttpClient client) => Client = client;
}

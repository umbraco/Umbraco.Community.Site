namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Marker type for the named HttpClient used by <see cref="FeedSubmissionImageProxyService"/>. Configured in
/// <c>RegisterFeeds</c> with redirects disabled and a <c>ConnectCallback</c> that pins the connection to a
/// pre-validated public IP — a dedicated client (rather than reusing <see cref="CommunityBlogsImageHttpClient"/>)
/// because those SSRF-hardening settings would be inappropriate for that client's own, non-caller-supplied use.
/// </summary>
public sealed class FeedSubmissionImageProxyHttpClient
{
    public HttpClient Client { get; }
    public FeedSubmissionImageProxyHttpClient(HttpClient client) => Client = client;
}

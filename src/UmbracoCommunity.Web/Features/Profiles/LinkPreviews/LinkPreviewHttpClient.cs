namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

/// <summary>
/// Marker type for the named HttpClient used by <see cref="LinkPreviewService"/>. Configured in
/// <c>RegisterMemberProfiles</c> with redirects disabled (the service follows them itself, checking every hop)
/// and a <c>ConnectCallback</c> that only connects to public addresses.
/// </summary>
public sealed class LinkPreviewHttpClient
{
    public HttpClient Client { get; }
    public LinkPreviewHttpClient(HttpClient client) => Client = client;
}

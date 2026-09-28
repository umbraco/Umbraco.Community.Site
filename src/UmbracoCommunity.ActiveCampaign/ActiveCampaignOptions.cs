namespace UmbracoCommunity.ActiveCampaign;

/// <summary>
/// Configuration for the ActiveCampaign subscribe workflow, bound to the <c>ActiveCampaign</c> section.
/// <see cref="BaseUrl"/> isn't a secret and is committed in <c>appsettings.json</c>. <see cref="ApiKey"/> ships empty;
/// the real one comes from <c>appsettings.Local.json</c> locally and from an environment variable / Cloud portal
/// secret when deployed (<c>ActiveCampaign__ApiKey</c>) — never the repo.
/// </summary>
/// <remarks>
/// These live in configuration rather than on the workflow's settings because Forms serialises workflow settings
/// with the form (Deploy transfers, the backoffice API), and an API key must not travel with it.
/// </remarks>
public sealed class ActiveCampaignOptions
{
    public const string SectionName = "ActiveCampaign";

    /// <summary>
    /// The account's v3 API root, e.g. <c>https://umbraco.api-us1.com/api/3/</c>
    /// (ActiveCampaign → Settings → Developer). A missing trailing slash is tolerated.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Sent as the <c>Api-Token</c> header. Secret — never logged.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>True when both values are present and <see cref="BaseUrl"/> is an absolute http(s) URL.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && TryGetBaseUri(out _);

    /// <summary>
    /// <see cref="BaseUrl"/> as a URI that always ends in <c>/</c>, so relative endpoint paths append to it.
    /// </summary>
    /// <remarks>
    /// <see cref="Uri(Uri, string)"/> follows RFC 3986: without the trailing slash, combining
    /// <c>https://x.api-us1.com/api/3</c> with <c>contact/sync</c> would <em>replace</em> the last segment and
    /// give <c>https://x.api-us1.com/api/contact/sync</c>. Endpoint paths must therefore never start with a slash either,
    /// or they would replace the whole path.
    /// </remarks>
    public bool TryGetBaseUri(out Uri baseUri)
    {
        baseUri = null!;
        if (string.IsNullOrWhiteSpace(BaseUrl)) return false;

        var normalised = BaseUrl.Trim().TrimEnd('/') + "/";
        if (!Uri.TryCreate(normalised, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

        baseUri = uri;
        return true;
    }
}

namespace UmbracoCommunity.Web.Features.OpenBadges;

/// <summary>
/// Paths of the Open Badges endpoints (mapped in <c>UseApplicationEndpoints</c>), and the absolute URLs built from them.
/// </summary>
public static class OpenBadgesRoutes
{
    public const string IssuerPattern = "badges/issuer.json";
    public const string MvpBadgeClassPattern = "badges/mvp/{year:int}.json";
    public const string MvpImagePattern = "badges/mvp/{year:int}.png";
    public const string AssertionPattern = "badges/assertions/{id}.json";
    public const string MvpDownloadPattern = "account/badges/umbraco-mvp-{year:int}.png";

    public static class Names
    {
        public const string Issuer = "OpenBadgesIssuer";
        public const string MvpBadgeClass = "OpenBadgesMvpBadgeClass";
        public const string MvpImage = "OpenBadgesMvpImage";
        public const string Assertion = "OpenBadgesAssertion";
        public const string MvpDownload = "OpenBadgesMvpDownload";
    }

    public const string Controller = "OpenBadges";

    public static string IssuerUrl(Uri baseUri) => new Uri(baseUri, "/badges/issuer.json").ToString();

    public static string MvpBadgeClassUrl(Uri baseUri, int year) => new Uri(baseUri, $"/badges/mvp/{year}.json").ToString();

    public static string MvpImageUrl(Uri baseUri, int year) => new Uri(baseUri, MvpImagePath(year)).ToString();

    /// <summary>Site-relative path of the public (not baked) badge image.</summary>
    public static string MvpImagePath(int year) => $"/badges/mvp/{year}.png";

    public static string AssertionUrl(Uri baseUri, string id) => new Uri(baseUri, $"/badges/assertions/{id}.json").ToString();

    /// <summary>Site-relative, for the account page link.</summary>
    public static string MvpDownloadPath(int year) => $"/account/badges/{MvpDownloadFileName(year)}";

    public static string MvpDownloadFileName(int year) => $"umbraco-mvp-{year}.png";
}

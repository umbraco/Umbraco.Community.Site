using Microsoft.AspNetCore.WebUtilities;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Adds UTM parameters to outbound community blog links so the target sites can see
/// that the traffic came from this site. Never overwrites UTM parameters the post's
/// own URL already carries.
/// </summary>
public static class OutboundLinkTagger
{
    public const string Medium = "referral";
    public const string Campaign = "community-blogs";

    public static string WithUtm(string url, string source)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(source))
        {
            return url;
        }

        var existing = QueryHelpers.ParseQuery(uri.Query);
        var additions = new Dictionary<string, string?>
        {
            ["utm_source"] = source,
            ["utm_medium"] = Medium,
            ["utm_campaign"] = Campaign,
        }
        .Where(kv => !existing.ContainsKey(kv.Key));

        var withQuery = QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Path) + uri.Query, additions);
        return withQuery + uri.Fragment;
    }
}

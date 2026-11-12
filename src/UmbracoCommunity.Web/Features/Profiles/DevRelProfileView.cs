using Microsoft.AspNetCore.Http;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The request and response side of a DevRel member viewing another member's profile (see
/// <see cref="ProfileAccessService.IsDevRelViewAsync"/>): the query string switch between the profile as the public
/// sees it and everything the member hid, and the response headers that keep the page out of shared caches.
/// </summary>
public static class DevRelProfileView
{
    /// <summary>The query string key that switches the view.</summary>
    public const string QueryKey = "devrel";

    /// <summary>The value of <see cref="QueryKey"/> that shows everything, including what the member hid.</summary>
    public const string ShowEverythingValue = "all";

    /// <summary>True when the request asks for everything, including the items and sections the member hid.</summary>
    public static bool IsShowEverythingRequested(IQueryCollection query) =>
        string.Equals(query[QueryKey].ToString(), ShowEverythingValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>The profile page as the public sees it: the path without a query string.</summary>
    public static string PublicViewUrl(PathString path) => path.HasValue ? path.Value! : "/";

    /// <summary>The profile page with everything shown, including what the member hid.</summary>
    public static string ShowEverythingUrl(PathString path) =>
        PublicViewUrl(path) + QueryString.Create(QueryKey, ShowEverythingValue);

    /// <summary>Stops browsers and shared caches from storing the page.</summary>
    public static void PreventCaching(HttpResponse response) =>
        response.Headers.CacheControl = "private, no-store";
}

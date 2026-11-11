using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace UmbracoCommunity.SearchTracker.Recording;

/// <summary>Identifies who is searching, for bot filtering and per-visitor limits. Nothing here is stored.</summary>
public static partial class SearchClient
{
    public static bool IsBot(HttpContext httpContext)
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) || BotUserAgentRegex().IsMatch(userAgent);
    }

    // Proxies and CDNs put the visitor's address in a header; the user agent keeps people behind one address apart.
    public static string GetKey(HttpContext httpContext)
    {
        var headers = httpContext.Request.Headers;
        var address = FirstAddress(headers["CF-Connecting-IP"].ToString())
            ?? FirstAddress(headers["X-Forwarded-For"].ToString())
            ?? httpContext.Connection.RemoteIpAddress?.ToString();
        return $"{address}|{headers.UserAgent}";
    }

    private static string? FirstAddress(string header)
    {
        var first = header.Split(',')[0].Trim();
        if (IPAddress.TryParse(first, out var address)) return address.ToString();
        return IPEndPoint.TryParse(first, out var endPoint) ? endPoint.Address.ToString() : null;
    }

    [GeneratedRegex(@"bot|crawl|spider|slurp|scrape|preview|headless|lighthouse|monitor|python|curl|wget|httpclient|java/|go-http|okhttp|axios|node-fetch|postman",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BotUserAgentRegex();
}

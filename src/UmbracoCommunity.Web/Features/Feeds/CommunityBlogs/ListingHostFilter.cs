namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Matches post URLs against <see cref="CommunityBlogsOptions.ExcludedListingHosts"/>. Hosts are
/// compared case-insensitively, ignoring a leading "www."; a scheme/port/path on either side is
/// tolerated and ignored.
/// </summary>
public static class ListingHostFilter
{
    /// <summary>
    /// True when <paramref name="url"/> is an absolute URL whose host is one of
    /// <paramref name="excludedHosts"/>. Unparseable or relative URLs return false, so those posts are kept.
    /// </summary>
    public static bool IsExcluded(string? url, IEnumerable<string>? excludedHosts)
    {
        if (excludedHosts is null || string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return false;

        var host = NormalizeHost(uri.Host);
        return host is not null && excludedHosts.Any(h => string.Equals(NormalizeHost(h), host, StringComparison.Ordinal));
    }

    /// <summary>
    /// Extracts the host from a bare host ("community.umbraco.com") or URL-ish value
    /// ("https://www.community.umbraco.com:443/blog"), lower-cased with a leading "www." and
    /// trailing dot removed. Returns null when no host can be parsed.
    /// </summary>
    public static string? NormalizeHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "http://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return null;

        var host = uri.Host.TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return host.Length == 0 ? null : host;
    }
}

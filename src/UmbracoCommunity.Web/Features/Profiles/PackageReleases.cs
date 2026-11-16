using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Reads which package and version a release address (e.g. <c>https://www.nuget.org/packages/Cultiv.Hangfire/5.5.2</c>)
/// is for. Anything that isn't a NuGet package address has no package id or version here.
/// </summary>
public static partial class PackageReleases
{
    /// <summary>The package id (lower-cased) of a NuGet package address, or null.</summary>
    public static string? IdOf(string? url) => Segments(url) is { } s ? s.Id : null;

    /// <summary>The version of a NuGet package address that names one, or null.</summary>
    public static string? VersionOf(string? url) => Segments(url) is { Version: { } v } ? v : null;

    /// <summary>True when two versions are the same, ignoring case and build metadata (<c>+...</c>).</summary>
    public static bool SameVersion(string? a, string? b) =>
        a != null && b != null
        && string.Equals(a.Split('+')[0], b.Split('+')[0], StringComparison.OrdinalIgnoreCase);

    /// <summary>NuGet package ids: letters, digits, dots, dashes and underscores. Checked before an id goes into an address.</summary>
    public static bool IsValidId(string id) => id.Length is > 0 and <= 128 && ValidId().IsMatch(id);

    private static (string Id, string? Version)? Segments(string? url)
    {
        if (url == null
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.EndsWith("nuget.org", StringComparison.OrdinalIgnoreCase)
            || uri.Segments.Length < 3
            || !uri.Segments[1].Equals("packages/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var id = uri.Segments[2].TrimEnd('/');
        var version = uri.Segments.Length > 3 ? uri.Segments[3].TrimEnd('/') : null;
        return IsValidId(id) ? (id.ToLowerInvariant(), version) : null;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex ValidId();
}

/// <summary>Finds the first version a package was ever released with, so its first release can be told from a routine one.</summary>
public interface IPackageFirstVersionLookup
{
    /// <summary>The package's first version, or null when it isn't known (not found, or NuGet could not be reached).</summary>
    Task<string?> GetFirstVersionAsync(string packageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IPackageFirstVersionLookup"/> backed by NuGet's package index (<c>v3-flatcontainer</c>), which lists a
/// package's versions oldest first. Only the package id goes out. A first version never changes, so it is kept for a week;
/// "not known" only briefly, so a hiccup at NuGet doesn't hide a new package for long.
/// </summary>
public sealed class NuGetFirstVersionLookup : IPackageFirstVersionLookup
{
    private const string CacheKeyPrefix = "NuGetFirstVersion:";
    private static readonly TimeSpan KnownFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan UnknownFor = TimeSpan.FromMinutes(15);

    private readonly HttpClient _http;
    private readonly AppCaches _appCaches;
    private readonly ILogger<NuGetFirstVersionLookup> _logger;

    public NuGetFirstVersionLookup(HttpClient http, AppCaches appCaches, ILogger<NuGetFirstVersionLookup> logger)
    {
        _http = http;
        _appCaches = appCaches;
        _logger = logger;
    }

    public async Task<string?> GetFirstVersionAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var id = packageId.ToLowerInvariant();
        if (!PackageReleases.IsValidId(id))
        {
            return null;
        }

        var key = CacheKeyPrefix + id;
        if (_appCaches.RuntimeCache.Get(key) is Known cached)
        {
            return cached.Version;
        }

        string? first = null;
        try
        {
            var index = await _http.GetFromJsonAsync<VersionIndex>($"v3-flatcontainer/{id}/index.json", cancellationToken);
            first = index?.Versions?.FirstOrDefault();
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Could not read the version list of package {PackageId} from NuGet.", id);
        }

        _appCaches.RuntimeCache.Insert(key, () => new Known(first), first != null ? KnownFor : UnknownFor);
        return first;
    }

    private sealed record Known(string? Version);

    private sealed record VersionIndex(IReadOnlyList<string>? Versions);
}

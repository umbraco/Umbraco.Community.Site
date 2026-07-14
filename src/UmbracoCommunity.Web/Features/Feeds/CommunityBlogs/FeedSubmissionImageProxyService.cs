using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Fetches a remote image server-side so the feed-submission preview can render cover/avatar images from
/// whatever domain a submitted feed happens to be on without relaxing CSP img-src (unlike the persisted
/// Community Blogs grid, there's no content to permanently localize here — this exists purely to let the
/// browser load the bytes from 'self'). Downloads are cached briefly in memory so repeated "Check again"
/// clicks and multiple cards from the same author don't re-fetch the same URL.
///
/// This is a caller-supplied-URL proxy, so it's guarded against being turned into an open image relay or an
/// SSRF probe. Authorization is a self-contained, tamper-proof <see cref="IDataProtector"/>-signed token
/// (created via <see cref="CreateProxyUrl"/> at the moment a URL is put in front of a client, verified via
/// <see cref="FetchAsync"/>) rather than a server-side "recently seen" allowlist keyed by client IP — behind a
/// reverse proxy/CDN (e.g. Azure Front Door in front of Umbraco Cloud) or more than one app instance,
/// <c>RemoteIpAddress</c> isn't reliably the same value across the preview call and the image requests it
/// triggers, so an IP-keyed allowlist silently 404s every image in production even though it works locally.
/// The actual fetch pins the connection to a pre-validated public IP via <see cref="PublicNetworkGuard"/>
/// (both directly and via the ConnectCallback configured in <c>RegisterFeeds</c>) so there's no DNS-rebinding
/// window, and redirects are disabled end-to-end (including in the curl fallback) so a same-request redirect
/// to an internal address can't bypass the IP check either.
/// </summary>
public sealed class FeedSubmissionImageProxyService
{
    private const string DataProtectionPurpose = "UmbracoCommunity.FeedSubmissionImageProxy.v1";
    private const long MaxBytes = 5_000_000;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/tiff", "image/svg+xml",
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IDataProtector _protector;
    private readonly ILogger<FeedSubmissionImageProxyService> _logger;

    public FeedSubmissionImageProxyService(
        FeedSubmissionImageProxyHttpClient typedClient,
        IMemoryCache cache,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<FeedSubmissionImageProxyService> logger)
    {
        _http = typedClient.Client;
        _cache = cache;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _logger = logger;
    }

    /// <summary>
    /// Builds the proxied URL for an absolute image URL that just appeared in a preview response — a signed
    /// token carrying the source URL, so <see cref="FetchAsync"/> can later verify it without any shared state.
    /// Returns null unchanged if <paramref name="absoluteUrl"/> is null/empty.
    /// </summary>
    public string? CreateProxyUrl(string? absoluteUrl)
    {
        if (string.IsNullOrEmpty(absoluteUrl))
        {
            return null;
        }

        var token = _protector.Protect(absoluteUrl);
        return $"/api/feed-submission/image-proxy?token={Uri.EscapeDataString(token)}";
    }

    /// <summary>Returns the image bytes and content type for the URL encoded in <paramref name="token"/>, or null if it can't be safely fetched.</summary>
    public async Task<(byte[] Bytes, string ContentType)?> FetchAsync(string token, CancellationToken cancellationToken)
    {
        string url;
        try
        {
            url = _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning("Refusing to proxy: token failed to unprotect (tampered, expired key ring, or not ours).");
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var cacheKey = $"feed-submission-image:{uri}";
        if (_cache.TryGetValue(cacheKey, out (byte[] Bytes, string ContentType) cached))
        {
            return cached;
        }

        var address = await PublicNetworkGuard.ResolvePublicAddressAsync(uri.Host, cancellationToken);
        if (address is null)
        {
            _logger.LogWarning("Refusing to proxy {Uri}: host does not resolve to a public address.", uri);
            return null;
        }

        var result = await DownloadAsync(uri, address, cancellationToken);
        if (result is not null)
        {
            _cache.Set(cacheKey, result.Value, CacheDuration);
        }

        return result;
    }

    private async Task<(byte[] Bytes, string ContentType)?> DownloadAsync(Uri uri, IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            if (response.Content.Headers.ContentLength is long declared && declared > MaxBytes)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !AllowedContentTypes.Contains(contentType))
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0 || bytes.Length > MaxBytes)
            {
                return null;
            }

            return (bytes, contentType);
        }
        catch (HttpRequestException ex)
        {
            // Some blogger-hosted images sit behind a host/WAF that blocks .NET's TLS ClientHello
            // fingerprint outright (see CommunityBlogsImageDownloader) — fall back to curl, still pinned
            // to the same pre-validated IP and still refusing redirects.
            _logger.LogDebug(ex, "HttpClient download failed for {Uri}; trying curl fallback.", uri);
            return await TryDownloadViaCurlAsync(uri, address, cancellationToken);
        }
    }

    private async Task<(byte[] Bytes, string ContentType)?> TryDownloadViaCurlAsync(Uri uri, IPAddress address, CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"feed-submission-image-{Guid.NewGuid():N}.tmp");
        try
        {
            var startInfo = new ProcessStartInfo("curl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-sS");
            // No -L: redirects are refused here for the same reason the HttpClient path disables them —
            // a redirect target is a fresh, unvalidated destination, and it would otherwise sail past the
            // pinned-IP protection just added below.
            startInfo.ArgumentList.Add("--max-time");
            startInfo.ArgumentList.Add("10");
            startInfo.ArgumentList.Add("--max-filesize");
            startInfo.ArgumentList.Add(MaxBytes.ToString());
            // Pins the connection to the same pre-validated IP the HttpClient path uses (closing the
            // DNS-rebinding window here too), while keeping the original hostname for the Host header/SNI.
            startInfo.ArgumentList.Add("--resolve");
            startInfo.ArgumentList.Add($"{uri.Host}:{(uri.IsDefaultPort ? (uri.Scheme == Uri.UriSchemeHttps ? 443 : 80) : uri.Port)}:{address}");
            startInfo.ArgumentList.Add("-A");
            startInfo.ArgumentList.Add("UmbracoCommunitySite/1.0 (+https://community.umbraco.com)");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(tempFile);
            startInfo.ArgumentList.Add("-w");
            startInfo.ArgumentList.Add("%{content_type}");
            startInfo.ArgumentList.Add(uri.ToString());

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return null;
            }

            if (process.ExitCode != 0 || !File.Exists(tempFile))
            {
                return null;
            }

            var contentType = (await stdOutTask).Trim();
            if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(tempFile, cancellationToken);
            if (bytes.Length == 0 || bytes.Length > MaxBytes)
            {
                return null;
            }

            return (bytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "curl fallback failed for {Uri}.", uri);
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Best-effort cleanup; a stray temp file isn't worth failing the request over.
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort; the process will be reaped by the OS regardless.
        }
    }
}

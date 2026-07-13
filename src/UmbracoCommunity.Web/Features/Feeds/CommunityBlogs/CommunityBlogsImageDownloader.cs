using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Downloads community blog post images (cover + author avatar) to a web-served folder under
/// wwwroot and rewrites each post's image URLs to the local path, so they load from 'self'
/// (no CSP relaxation needed). Failed/invalid downloads become null. Unreferenced files are pruned.
/// </summary>
public sealed class CommunityBlogsImageDownloader
{
    /// <summary>Web-served subfolder under wwwroot. Served at "/community-blog-images/{file}".</summary>
    public const string SubFolder = "community-blog-images";
    private const long MaxBytes = 10_000_000;

    private static readonly Dictionary<string, string> ExtByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["image/tiff"] = ".tiff",
    };

    private readonly HttpClient _http;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CommunityBlogsImageDownloader> _logger;

    public CommunityBlogsImageDownloader(
        CommunityBlogsImageHttpClient typedClient,
        IWebHostEnvironment env,
        ILogger<CommunityBlogsImageDownloader> logger)
    {
        _http = typedClient.Client;
        _env = env;
        _logger = logger;
    }

    /// <summary>Downloads every post's images locally and returns data with rewritten (local or null) image URLs.</summary>
    public async Task<CommunityBlogsData> LocalizeAsync(CommunityBlogsData data, CancellationToken cancellationToken)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            _logger.LogWarning("WebRootPath is unavailable; skipping community blog image localization.");
            return data;
        }

        var dir = Path.Combine(webRoot, SubFolder);
        Directory.CreateDirectory(dir);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localized = new List<CommunityBlogPost>(data.Posts.Count);

        foreach (var post in data.Posts)
        {
            var cover = await EnsureLocalAsync(post.CoverImageUrl, dir, used, cancellationToken);
            var avatar = await EnsureLocalAsync(post.AuthorAvatarUrl, dir, used, cancellationToken);
            localized.Add(post with { CoverImageUrl = cover, AuthorAvatarUrl = avatar });
        }

        Prune(dir, used);
        return data with { Posts = localized };
    }

    private async Task<string?> EnsureLocalAsync(string? url, string dir, HashSet<string> used, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // Idempotent: an already-local path is kept and marked as used.
        var localPrefix = "/" + SubFolder + "/";
        if (url.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            used.Add(Path.GetFileName(url));
            return url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();

            // Dedup: reuse an already-downloaded file for this URL.
            var existing = Directory.EnumerateFiles(dir, hash + ".*").FirstOrDefault();
            if (existing is not null)
            {
                var existingName = Path.GetFileName(existing);
                used.Add(existingName);
                return localPrefix + existingName;
            }

            var download = await DownloadAsync(uri, cancellationToken);
            if (download is null)
            {
                return null;
            }

            var (bytes, contentType) = download.Value;

            if (contentType is null || !ExtByContentType.TryGetValue(contentType, out var ext))
            {
                return null;
            }

            if (bytes.Length == 0 || bytes.Length > MaxBytes)
            {
                return null;
            }

            var fileName = hash + ext;
            await File.WriteAllBytesAsync(Path.Combine(dir, fileName), bytes, cancellationToken);
            used.Add(fileName);
            return localPrefix + fileName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download community blog image {Url}.", url);
            return null;
        }
    }

    /// <summary>
    /// Downloads via HttpClient first. Some blogger-hosted images sit behind a host/WAF that blocks
    /// .NET's TLS ClientHello fingerprint outright (not a protocol-version issue — confirmed by
    /// testing TLS 1.2/1.3 explicitly, both reset) while plain curl and real browsers work fine
    /// against the same host. Falling back to curl as a subprocess recovers those images.
    /// </summary>
    private async Task<(byte[] Bytes, string? ContentType)?> DownloadAsync(Uri uri, CancellationToken cancellationToken)
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

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (bytes, response.Content.Headers.ContentType?.MediaType);
        }
        catch (HttpRequestException)
        {
            // Expected and already handled by the curl fallback below (some hosts block .NET's TLS
            // fingerprint outright) — not worth a warning/stack trace on its own; TryDownloadViaCurlAsync
            // logs if the fallback also fails, which is the actually-actionable case.
            return await TryDownloadViaCurlAsync(uri, cancellationToken);
        }
    }

    private async Task<(byte[] Bytes, string? ContentType)?> TryDownloadViaCurlAsync(Uri uri, CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"community-blog-image-{Guid.NewGuid():N}.tmp");
        try
        {
            var startInfo = new ProcessStartInfo("curl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // No shell involved (ArgumentList execs curl directly), so the URL can't be interpreted
            // as shell syntax. -w prints the content-type after the transfer so we don't need a
            // separate HEAD request; -o writes the body to disk to avoid corrupting binary data via
            // stdout text encoding.
            startInfo.ArgumentList.Add("-sS");
            startInfo.ArgumentList.Add("-L");
            startInfo.ArgumentList.Add("--max-time");
            startInfo.ArgumentList.Add("20");
            startInfo.ArgumentList.Add("--max-filesize");
            startInfo.ArgumentList.Add(MaxBytes.ToString());
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));

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
            var bytes = await File.ReadAllBytesAsync(tempFile, cancellationToken);

            _logger.LogInformation("Recovered image download via curl fallback for {Uri}.", uri);
            return (bytes, string.IsNullOrWhiteSpace(contentType) ? null : contentType);
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

    private void Prune(string dir, HashSet<string> used)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (!used.Contains(Path.GetFileName(file)))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prune community blog images in {Dir}.", dir);
        }
    }
}

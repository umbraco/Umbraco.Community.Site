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

            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !ExtByContentType.TryGetValue(contentType, out var ext))
            {
                return null;
            }

            if (response.Content.Headers.ContentLength is long declared && declared > MaxBytes)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
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

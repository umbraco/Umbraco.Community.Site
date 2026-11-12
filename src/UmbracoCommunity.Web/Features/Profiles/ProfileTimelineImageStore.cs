using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.IO;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Keeps our own copy of the pictures on timeline preview cards, so a card is served from this site and keeps
/// working when the picture's own site changes or disappears. Copies live in the media file system (the same
/// storage as uploaded avatars, so blob storage on Umbraco Cloud) under <see cref="Folder"/>, named by a hash of
/// the picture's address: each picture is stored once and reused. A picture is downloaded only over https from a
/// public address (every redirect hop re-checked), within a time limit, up to <see cref="MaxBytes"/>, and only when
/// both the response and the file itself are a JPEG, PNG, GIF or WebP image; anything else is refused.
/// </summary>
public sealed class ProfileTimelineImageStore
{
    /// <summary>The folder in the media file system that holds the copies.</summary>
    public const string Folder = "profile-timeline";

    /// <summary>The largest picture kept.</summary>
    public const int MaxBytes = 2_000_000;

    private const int MaxRedirects = 3;
    private const string IndexCacheKey = "ProfileTimelineImage:index";
    private const string FailedCacheKeyPrefix = "ProfileTimelineImageFailed:";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan IndexCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan FailedCacheDuration = TimeSpan.FromMinutes(15);

    private static readonly (string Extension, string MediaType)[] Formats =
    [
        (".jpg", "image/jpeg"),
        (".png", "image/png"),
        (".gif", "image/gif"),
        (".webp", "image/webp"),
    ];

    private static readonly HashSet<string> AcceptedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp",
    };

    private readonly IFileSystem _fileSystem;
    private readonly HttpClient _http;
    private readonly AppCaches _appCaches;
    private readonly ILogger<ProfileTimelineImageStore> _logger;

    public ProfileTimelineImageStore(
        IFileSystem fileSystem,
        LinkPreviewHttpClient typedClient,
        AppCaches appCaches,
        ILogger<ProfileTimelineImageStore> logger)
    {
        _fileSystem = fileSystem;
        _http = typedClient.Client;
        _appCaches = appCaches;
        _logger = logger;
    }

    /// <summary>The address our copy of <paramref name="sourceUrl"/> is served at, or null when there is none yet.</summary>
    public string? FindStored(string sourceUrl) =>
        Index().TryGetValue(FileName(sourceUrl), out var path) ? _fileSystem.GetUrl(path) : null;

    /// <summary>
    /// The stored copies by file name, read from the folder once and then kept in memory (and added to as copies are
    /// stored), so finding a copy doesn't ask the file system each time. Copies stored by another server show up when
    /// the list is next read.
    /// </summary>
    private ConcurrentDictionary<string, string> Index() =>
        _appCaches.RuntimeCache.GetCacheItem(
            IndexCacheKey,
            () =>
            {
                var index = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
                if (_fileSystem.DirectoryExists(Folder))
                {
                    foreach (var path in _fileSystem.GetFiles(Folder))
                    {
                        index[Path.GetFileNameWithoutExtension(path)] = path;
                    }
                }

                return index;
            },
            IndexCacheDuration)!;

    /// <summary>
    /// Our copy of <paramref name="sourceUrl"/>: the one already stored, otherwise a fresh download. Null when the
    /// picture can't be fetched or isn't an accepted image; that answer is kept for a while, so a failing picture
    /// isn't fetched again on every render.
    /// </summary>
    public async Task<string?> GetOrStoreAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        if (FindStored(sourceUrl) is { } stored)
        {
            return stored;
        }

        if (_appCaches.RuntimeCache.Get(FailedCacheKeyPrefix + sourceUrl) is not null)
        {
            return null;
        }

        var image = await DownloadAsync(sourceUrl, cancellationToken);
        if (image == null)
        {
            _appCaches.RuntimeCache.Insert(FailedCacheKeyPrefix + sourceUrl, () => true, FailedCacheDuration);
            return null;
        }

        var path = $"{Folder}/{FileName(sourceUrl)}{image.Value.Extension}";
        using (var content = new MemoryStream(image.Value.Bytes, writable: false))
        {
            _fileSystem.AddFile(path, content, overrideIfExists: true);
        }

        Index()[FileName(sourceUrl)] = path;
        return _fileSystem.GetUrl(path);
    }

    /// <summary>The file name (without extension) for a picture's address: a hash, so nothing from the address reaches the path.</summary>
    public static string FileName(string sourceUrl) => ProfileTimelinePreviewStore.Hash(sourceUrl)[..32];

    /// <summary>The accepted image format <paramref name="bytes"/> start with, or null.</summary>
    public static (string Extension, string MediaType)? Sniff(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return Formats[0];
        }

        if (bytes.Length >= 8 && bytes[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Formats[1];
        }

        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
        {
            return Formats[2];
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return Formats[3];
        }

        return null;
    }

    private async Task<(byte[] Bytes, string Extension)?> DownloadAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        if (!ProfileLinkUrl.TryParse(sourceUrl, out var uri))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                if (await PublicNetworkGuard.ResolvePublicAddressAsync(uri.Host, timeout.Token) is null)
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.ParseAdd("image/jpeg, image/png, image/gif, image/webp");
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (response.Headers.Location is null
                        || !ProfileLinkUrl.TryParse(new Uri(uri, response.Headers.Location).ToString(), out uri))
                    {
                        return null;
                    }

                    continue;
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!response.IsSuccessStatusCode
                    || mediaType is null
                    || !AcceptedMediaTypes.Contains(mediaType)
                    || response.Content.Headers.ContentLength > MaxBytes)
                {
                    return null;
                }

                var bytes = await ReadLimitedAsync(response, timeout.Token);
                return bytes != null && Sniff(bytes) is { } format ? (bytes, format.Extension) : null;
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or UriFormatException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogDebug(ex, "Could not copy a timeline picture from {Host}.", uri.Host);
            return null;
        }
    }

    /// <summary>The body, or null when it is empty or larger than <see cref="MaxBytes"/>.</summary>
    private static async Task<byte[]?> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total is 0 or > MaxBytes ? null : buffer[..total];
    }
}

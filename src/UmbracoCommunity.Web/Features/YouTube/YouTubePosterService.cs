using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Helpers;
using CoreConstants = Umbraco.Cms.Core.Constants;

namespace UmbracoCommunity.Web.Features.YouTube;

public sealed record YouTubeStill(string Variant, string Label, string Url);

public sealed record YouTubeVideoPreview(string VideoId, string? Title, string? Author, IReadOnlyList<YouTubeStill> Stills);

public sealed record YouTubePoster(Guid MediaKey, string Name, string? Url);

public interface IYouTubePosterService
{
    /// <summary>The video's title and up to four stills to choose a poster from, or <c>null</c> for a link that isn't a YouTube video.</summary>
    Task<YouTubeVideoPreview?> GetPreviewAsync(string url, CancellationToken cancellationToken);

    /// <summary>Copies a still into <c>Video thumbnails/&lt;site name&gt;</c> in the Media library, reusing an earlier import of the same still.</summary>
    Task<YouTubePoster?> ImportStillAsync(string url, string variant, Guid? contentKey, string? title, int userId, CancellationToken cancellationToken);

    YouTubePoster? GetPoster(Guid mediaKey);
}

internal sealed class YouTubePosterService : IYouTubePosterService
{
    public const string HttpClientName = "YouTube";
    public const string RootFolderName = "Video thumbnails";
    private const string SharedFolderName = "Shared";
    private const int MaxImageBytes = 5 * 1024 * 1024;

    // Each slot lists its image from best to worst; the 1280px versions only exist for HD videos.
    private static readonly (string Label, string[] Variants)[] Slots =
    [
        ("YouTube thumbnail", ["maxresdefault", "sddefault", "hqdefault"]),
        ("Still 1", ["maxres1", "sd1", "hq1"]),
        ("Still 2", ["maxres2", "sd2", "hq2"]),
        ("Still 3", ["maxres3", "sd3", "hq3"]),
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaService _mediaService;
    private readonly IContentService _contentService;
    private readonly MediaFileManager _mediaFileManager;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
    private readonly ILogger<YouTubePosterService> _logger;

    public YouTubePosterService(
        IHttpClientFactory httpClientFactory,
        IMediaService mediaService,
        IContentService contentService,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        ILogger<YouTubePosterService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _mediaService = mediaService;
        _contentService = contentService;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _logger = logger;
    }

    internal static string StillUrl(string videoId, string variant) => $"https://i.ytimg.com/vi/{videoId}/{variant}.jpg";

    internal static string? LabelFor(string variant)
        => Slots.FirstOrDefault(slot => slot.Variants.Contains(variant, StringComparer.Ordinal)).Label;

    public async Task<YouTubeVideoPreview?> GetPreviewAsync(string url, CancellationToken cancellationToken)
    {
        if (VideoHelper.GetYouTubeVideoId(url) is not { } videoId) return null;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var stillsTask = Task.WhenAll(Slots.Select(slot => FirstAvailableStillAsync(client, videoId, slot, cancellationToken)));
        var detailsTask = GetDetailsAsync(client, videoId, cancellationToken);

        var stills = (await stillsTask).OfType<YouTubeStill>().ToList();
        var (title, author) = await detailsTask;
        return new YouTubeVideoPreview(videoId, title, author, stills);
    }

    public async Task<YouTubePoster?> ImportStillAsync(string url, string variant, Guid? contentKey, string? title, int userId, CancellationToken cancellationToken)
    {
        if (VideoHelper.GetYouTubeVideoId(url) is not { } videoId || LabelFor(variant) is not { } label) return null;

        var siteFolder = GetOrCreateFolder(GetSiteName(contentKey), GetOrCreateFolder(RootFolderName, null, userId).Id, userId);
        var name = $"{(string.IsNullOrWhiteSpace(title) ? videoId : title.Trim())} – {label}";

        var existing = GetChildren(siteFolder.Id).FirstOrDefault(m =>
            m.ContentType.Alias == CoreConstants.Conventions.MediaTypes.Image && string.Equals(m.Name, name, StringComparison.Ordinal));
        if (existing is not null) return ToPoster(existing);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(StillUrl(videoId, variant), cancellationToken);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "image/jpeg")
        {
            _logger.LogWarning("Couldn't download YouTube still {Variant} for video {VideoId}: {Status}", variant, videoId, response.StatusCode);
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length is 0 or > MaxImageBytes) return null;

        var media = _mediaService.CreateMedia(name, siteFolder.Id, CoreConstants.Conventions.MediaTypes.Image, userId);
        using (var stream = new MemoryStream(bytes))
        {
            media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider,
                CoreConstants.Conventions.Media.File, $"youtube-{videoId}-{variant}.jpg", stream);
        }

        var result = _mediaService.Save(media, userId);
        if (!result.Success)
        {
            _logger.LogWarning("Couldn't save the YouTube poster for video {VideoId}: {Result}", videoId, result.Result?.Result);
            return null;
        }

        return ToPoster(media);
    }

    public YouTubePoster? GetPoster(Guid mediaKey)
        => _mediaService.GetById(mediaKey) is { Trashed: false } media ? ToPoster(media) : null;

    private static async Task<YouTubeStill?> FirstAvailableStillAsync(HttpClient client, string videoId, (string Label, string[] Variants) slot, CancellationToken cancellationToken)
    {
        foreach (var variant in slot.Variants)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, StillUrl(videoId, variant));
                using var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) return new YouTubeStill(variant, slot.Label, StillUrl(videoId, variant));
            }
            catch (HttpRequestException)
            {
            }
        }

        return null;
    }

    // oEmbed needs no API key; private or removed videos just come back without a title.
    private static async Task<(string? Title, string? Author)> GetDetailsAsync(HttpClient client, string videoId, CancellationToken cancellationToken)
    {
        try
        {
            var watchUrl = Uri.EscapeDataString($"https://www.youtube.com/watch?v={videoId}");
            using var response = await client.GetAsync($"https://www.youtube.com/oembed?url={watchUrl}&format=json", cancellationToken);
            if (!response.IsSuccessStatusCode) return (null, null);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return (Read(json, "title"), Read(json, "author_name"));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return (null, null);
        }

        static string? Read(JsonDocument json, string property)
            => json.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    // The site is the edited page's root node. New pages that aren't saved yet pass their parent instead.
    private string GetSiteName(Guid? contentKey)
    {
        if (contentKey is not { } key || _contentService.GetById(key) is not { } content) return SharedFolderName;

        var path = content.Path.Split(',');
        return path.Length > 1 && int.TryParse(path[1], out var rootId) && rootId > 0 && _contentService.GetById(rootId) is { } root
            ? root.Name ?? SharedFolderName
            : SharedFolderName;
    }

    private IMedia GetOrCreateFolder(string name, int? parentId, int userId)
    {
        var siblings = parentId is { } id ? GetChildren(id) : _mediaService.GetRootMedia();
        var folder = siblings.FirstOrDefault(m =>
            m.ContentType.Alias == CoreConstants.Conventions.MediaTypes.Folder && string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        if (folder is not null) return folder;

        folder = _mediaService.CreateMedia(name, parentId ?? CoreConstants.System.Root, CoreConstants.Conventions.MediaTypes.Folder, userId);
        _mediaService.Save(folder, userId);
        return folder;
    }

    private IEnumerable<IMedia> GetChildren(int parentId)
        => _mediaService.GetPagedChildren(parentId, 0, 1000, out _, null, null);

    private YouTubePoster ToPoster(IMedia media)
    {
        var property = media.Properties[CoreConstants.Conventions.Media.File];
        var url = property is not null && _mediaUrlGenerators.TryGetMediaPath(property.PropertyType.PropertyEditorAlias, property.GetValue(), out var path)
            ? path
            : null;
        return new YouTubePoster(media.Key, media.Name ?? string.Empty, url);
    }
}

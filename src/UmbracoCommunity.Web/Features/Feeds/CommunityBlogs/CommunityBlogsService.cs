using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public sealed class CommunityBlogsService : ICommunityBlogsService
{
    private const string PrimaryCacheKey = "community-blogs:primary";
    private const string StaleCacheKey = "community-blogs:stale";
    private static readonly TimeSpan StaleFallbackDuration = TimeSpan.FromDays(30);

    private readonly CommunityBlogsAggregator _aggregator;
    private readonly CommunityBlogsImageDownloader _imageDownloader;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly ILogger<CommunityBlogsService> _logger;
    private readonly string _cacheFilePath;

    public CommunityBlogsService(
        CommunityBlogsAggregator aggregator,
        CommunityBlogsImageDownloader imageDownloader,
        IMemoryCache cache,
        IOptionsMonitor<CommunityBlogsOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<CommunityBlogsService> logger)
    {
        _aggregator = aggregator;
        _imageDownloader = imageDownloader;
        _cache = cache;
        _options = options;
        _logger = logger;

        var cacheDir = Path.Combine(hostEnvironment.ContentRootPath, "umbraco", "Data", "TEMP", "CommunityBlogsCache");
        Directory.CreateDirectory(cacheDir);
        _cacheFilePath = Path.Combine(cacheDir, "community-blogs.json");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var data = await _aggregator.BuildAsync(cancellationToken);
        if (data is null)
        {
            _logger.LogInformation("Community blogs refresh produced no data; keeping existing cache.");
            return;
        }

        data = await _imageDownloader.LocalizeAsync(data, cancellationToken);

        var primaryDuration = TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RefreshIntervalInHours));
        _cache.Set(PrimaryCacheKey, data, primaryDuration);
        _cache.Set(StaleCacheKey, data, new MemoryCacheEntryOptions { SlidingExpiration = StaleFallbackDuration });

        await WriteCacheFileAsync(data, cancellationToken);
        _logger.LogInformation("Refreshed {Count} community blog posts.", data.Posts.Count);
    }

    public CommunityBlogsData GetData()
    {
        if (_cache.TryGetValue(PrimaryCacheKey, out CommunityBlogsData? primary) && primary is not null)
        {
            return primary;
        }

        // Disk is the durable cache. If a disk read fails or returns null (e.g. a corrupt
        // file, or a read racing a refresh write), we fall through to the stale in-memory
        // copy below as a backstop.
        var disk = TryReadCacheFile();
        if (disk is not null)
        {
            // Reseed the primary cache too, so subsequent requests in this window don't each
            // re-read the file synchronously until the next background refresh runs.
            var primaryDuration = TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RefreshIntervalInHours));
            _cache.Set(PrimaryCacheKey, disk, primaryDuration);
            _cache.Set(StaleCacheKey, disk, new MemoryCacheEntryOptions { SlidingExpiration = StaleFallbackDuration });
            return disk;
        }

        if (_cache.TryGetValue(StaleCacheKey, out CommunityBlogsData? stale) && stale is not null)
        {
            return stale;
        }

        return CommunityBlogsData.Empty;
    }

    public PagedCommunityBlogPosts GetPage(int page, int pageSize)
    {
        var data = GetData();
        pageSize = Math.Max(1, pageSize);

        var totalItems = data.Posts.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var clampedPage = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);

        var items = data.Posts
            .Skip((clampedPage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedCommunityBlogPosts(items, clampedPage, pageSize, totalItems, totalPages);
    }

    private async Task WriteCacheFileAsync(CommunityBlogsData data, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, SphereJsonOptions.Default);
            var tempPath = _cacheFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            // Atomic on the same filesystem: a concurrent reader sees either the old
            // file or the fully-written new one, never a partial write.
            File.Move(tempPath, _cacheFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write community blogs disk cache to {Path}.", _cacheFilePath);
        }
    }

    private CommunityBlogsData? TryReadCacheFile()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_cacheFilePath);
            return JsonSerializer.Deserialize<CommunityBlogsData>(json, SphereJsonOptions.Default);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read community blogs disk cache from {Path}.", _cacheFilePath);
            return null;
        }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.BlogAnnouncements.Detection;

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
    private readonly ICommunityBlogsIndexer _indexer;
    private readonly IBlogAnnouncementDetector? _announcementDetector;
    private readonly ILogger<CommunityBlogsService> _logger;
    private readonly string _cacheFilePath;

    public CommunityBlogsService(
        CommunityBlogsAggregator aggregator,
        CommunityBlogsImageDownloader imageDownloader,
        IMemoryCache cache,
        IOptionsMonitor<CommunityBlogsOptions> options,
        IHostEnvironment hostEnvironment,
        ICommunityBlogsIndexer indexer,
        ILogger<CommunityBlogsService> logger,
        IBlogAnnouncementDetector? announcementDetector = null)
    {
        _aggregator = aggregator;
        _imageDownloader = imageDownloader;
        _cache = cache;
        _options = options;
        _indexer = indexer;
        _announcementDetector = announcementDetector;
        _logger = logger;

        var cacheDir = Path.Combine(hostEnvironment.ContentRootPath, "umbraco", "Data", "TEMP", "CommunityBlogsCache");
        Directory.CreateDirectory(cacheDir);
        _cacheFilePath = Path.Combine(cacheDir, "community-blogs.json");
    }

    // Serialises refresh cycles: the periodic timer never overlaps itself (awaited loop), but the
    // dashboard's manual "Poll now" can arrive mid-cycle — it then waits its turn instead of
    // running the fetch/detection/localization pipeline concurrently.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            await RefreshCoreAsync(cancellationToken);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var data = await _aggregator.BuildAsync(cancellationToken);
        if (data is null)
        {
            // The aggregator produced nothing (e.g. the upstream API is down), but the
            // block still serves posts from the disk/stale cache via GetData(). Rebuild the
            // search index from that fallback so search results don't diverge from what the
            // block renders. (Rebuild is idempotent and cheap — the set is capped at ~60.)
            var fallback = GetData();
            if (fallback.Posts.Count > 0)
            {
                _indexer.Rebuild(fallback);
            }

            _logger.LogInformation("Community blogs refresh produced no data; keeping existing cache.");
            return;
        }

        // Run announcement detection on the RAW (pre-localization) data so Discord receives the
        // original remote image URLs — localization rewrites them to local paths Discord can't
        // fetch (and drops SVG avatars). Isolated so a detection failure never aborts the refresh.
        await DetectAnnouncementsSafelyAsync(data, cancellationToken);

        data = await _imageDownloader.LocalizeAsync(data, cancellationToken);

        var primaryDuration = TimeSpan.FromMinutes(Math.Max(5, _options.CurrentValue.RefreshIntervalInMinutes));
        _cache.Set(PrimaryCacheKey, data, primaryDuration);
        _cache.Set(StaleCacheKey, data, new MemoryCacheEntryOptions { SlidingExpiration = StaleFallbackDuration });

        await WriteCacheFileAsync(data, cancellationToken);
        _indexer.Rebuild(data);
        _logger.LogInformation("Refreshed {Count} community blog posts.", data.Posts.Count);
    }

    /// <summary>
    /// Runs Discord announcement detection, swallowing and logging any failure so the blog-posts
    /// cache refresh always completes. No-ops when the detector isn't registered.
    /// </summary>
    private async Task DetectAnnouncementsSafelyAsync(CommunityBlogsData data, CancellationToken cancellationToken)
    {
        if (_announcementDetector is null)
        {
            return;
        }

        try
        {
            // Map the site's blog-post shape onto the announcement project's neutral input record so
            // that project stays independent of UmbracoCommunity.Web. Field order mirrors the record.
            var candidates = data.Posts
                .Select(p => new AnnouncementCandidatePost(
                    p.Id,
                    p.Title,
                    p.Url,
                    p.Excerpt,
                    p.CoverImageUrl,
                    p.PublishedAt,
                    p.AuthorName,
                    p.AuthorAvatarUrl,
                    p.AuthorProfileUrl))
                .ToList();
            await _announcementDetector.DetectAndAnnounceAsync(candidates, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blog announcement detection failed; blog posts cache refresh continues.");
        }
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
            var primaryDuration = TimeSpan.FromMinutes(Math.Max(5, _options.CurrentValue.RefreshIntervalInMinutes));
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

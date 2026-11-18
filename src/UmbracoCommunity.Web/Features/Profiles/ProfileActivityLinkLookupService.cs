using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Reads the page behind a link a member is about to add by hand, so the add form can be filled in for them: title,
/// description, published date, picture and a guess at the kind. The page is read server side by
/// <see cref="LinkPreviewService"/> with all of its safeguards (https only, public addresses only, every redirect hop
/// re-checked, a time and size limit), never by the member's browser. The page's picture is copied by
/// <see cref="ProfileTimelineImageStore"/> and served from this site, exactly as a timeline card would (and the page's
/// details are kept in <see cref="ProfileTimelinePreviewStore"/>, so the entry's card later needs no outside work); the
/// picture is never downloaded and uploaded again by the browser. Only what was found is returned, cut to the entry
/// limits of <see cref="ProfileActivityEntry"/>, and anything that goes wrong gives <see cref="ActivityLinkDetails.Empty"/>
/// (the member then just types) rather than an exception.
/// </summary>
/// <remarks>
/// Because this makes the server fetch an address a member typed, each member may cause only
/// <see cref="MaxLookupsPerWindow"/> page reads per <see cref="LookupWindow"/> (see <see cref="ProfileActivityLinkLookupThrottle"/>).
/// That is a guard on outbound requests, not a limit on how much activity a member adds: answers are kept per address
/// for <see cref="CacheDuration"/> (a page that gave nothing for less) and an answer from that cache doesn't count.
/// </remarks>
public sealed class ProfileActivityLinkLookupService
{
    /// <summary>How many pages one member may have read per <see cref="LookupWindow"/>.</summary>
    public const int MaxLookupsPerWindow = 10;

    /// <summary>The window <see cref="MaxLookupsPerWindow"/> counts over.</summary>
    public static readonly TimeSpan LookupWindow = TimeSpan.FromMinutes(1);

    /// <summary>How long what a page offered is kept, per address.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan EmptyCacheDuration = TimeSpan.FromMinutes(2);

    private const string CacheKeyPrefix = "ProfileActivityLinkLookup:";
    private static readonly DateOnly EarliestDate = new(1990, 1, 1);

    private readonly LinkPreviewService _linkPreviewService;
    private readonly ProfileTimelineImageStore _imageStore;
    private readonly ProfileTimelinePreviewStore _previewStore;
    private readonly ProfileActivityLinkLookupThrottle _throttle;
    private readonly AppCaches _appCaches;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProfileActivityLinkLookupService> _logger;

    public ProfileActivityLinkLookupService(
        LinkPreviewService linkPreviewService,
        ProfileTimelineImageStore imageStore,
        ProfileTimelinePreviewStore previewStore,
        ProfileActivityLinkLookupThrottle throttle,
        AppCaches appCaches,
        TimeProvider timeProvider,
        ILogger<ProfileActivityLinkLookupService> logger)
    {
        _linkPreviewService = linkPreviewService;
        _imageStore = imageStore;
        _previewStore = previewStore;
        _throttle = throttle;
        _appCaches = appCaches;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>What the page at <paramref name="url"/> offers, for the member with <paramref name="memberKey"/>.</summary>
    public async Task<ActivityLinkDetails> LookUpAsync(Guid memberKey, string? url, CancellationToken cancellationToken = default)
    {
        if (!ProfileLinkUrl.TryParse(url, out var uri))
        {
            return ActivityLinkDetails.Empty;
        }

        var address = uri.AbsoluteUri;
        if (_appCaches.RuntimeCache.Get(CacheKeyPrefix + address) is ActivityLinkDetails cached)
        {
            return cached;
        }

        if (!_throttle.TryAcquire(memberKey))
        {
            return ActivityLinkDetails.Empty;
        }

        ActivityLinkDetails details;
        try
        {
            details = await ReadAsync(address, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Could not read the details of a link on {Host}", uri.Host);
            details = ActivityLinkDetails.Empty;
        }

        // A page that gave nothing may have been down for a moment, so that is kept for less time.
        _appCaches.RuntimeCache.Insert(
            CacheKeyPrefix + address,
            () => details,
            details == ActivityLinkDetails.Empty ? EmptyCacheDuration : CacheDuration);
        return details;
    }

    /// <summary>
    /// Our copies of the pictures of the pages at <paramref name="urls"/>, by page address, for the pages that have one
    /// kept. Nothing is fetched: a page without a kept picture is left out.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> StoredPagePicturesAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var pictures = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var (pageUrl, preview) in await _previewStore.GetAsync(urls, cancellationToken))
            {
                if (_imageStore.FindStored(preview.ImageUrl) is { } stored)
                {
                    pictures[pageUrl] = stored;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not look up the kept page pictures of a member's entries");
        }

        return pictures;
    }

    private async Task<ActivityLinkDetails> ReadAsync(string address, CancellationToken cancellationToken)
    {
        var preview = await _linkPreviewService.FetchAsync(address, cancellationToken);
        if (preview.Status != LinkPreviewStatus.Available)
        {
            return ActivityLinkDetails.Empty;
        }

        string? imageUrl = null;
        if (preview.ImageUrl != null)
        {
            imageUrl = await _imageStore.GetOrStoreAsync(preview.ImageUrl, cancellationToken);
            if (imageUrl != null)
            {
                await KeepForTimelineAsync(address, preview, cancellationToken);
            }
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        return new ActivityLinkDetails(
            GuessKind(new Uri(address), preview.PageType),
            Shorten(ProfileText.Normalize(preview.Title), ProfileActivityEntry.MaxTitleLength),
            preview.PublishedDate is { } date && date >= EarliestDate && date <= today ? date : null,
            Shorten(ProfileText.Normalize(preview.Description), ProfileActivityEntry.MaxDescriptionLength),
            imageUrl);
    }

    /// <summary>Keeps what the page offers, so the entry's timeline card finds it without reading the page again.</summary>
    private async Task KeepForTimelineAsync(string address, LinkPreview preview, CancellationToken cancellationToken)
    {
        try
        {
            await _previewStore.SaveAsync(address, preview.ImageUrl!, preview.Description, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not keep the timeline preview for a link on {Host}", new Uri(address).Host);
        }
    }

    /// <summary>
    /// A guess at what kind of activity the page is (one of <see cref="ProfileActivityEntry.Kinds"/>), or null: video
    /// sites are videos, package sites packages, and an <c>og:type</c> of <c>article</c> a blog post.
    /// </summary>
    public static string? GuessKind(Uri uri, string? pageType)
    {
        var host = uri.Host.ToLowerInvariant();
        bool On(string domain) => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

        if (On("youtube.com") || On("youtu.be") || On("vimeo.com"))
        {
            return ProfileActivityEntry.Video;
        }

        if (On("nuget.org") || On("marketplace.umbraco.com"))
        {
            return ProfileActivityEntry.Package;
        }

        if (pageType != null && pageType.StartsWith("video", StringComparison.Ordinal))
        {
            return ProfileActivityEntry.Video;
        }

        return pageType == "article" ? ProfileActivityEntry.Blog : null;
    }

    private static string? Shorten(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..(maxLength - 1)].TrimEnd() + "…" : value;
}

/// <summary>
/// Counts the page reads each member causes through <see cref="ProfileActivityLinkLookupService"/>, over a sliding
/// <see cref="ProfileActivityLinkLookupService.LookupWindow"/>, in memory on this server (a guard on outbound requests,
/// so per server is enough).
/// </summary>
public sealed class ProfileActivityLinkLookupThrottle
{
    private readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> _reads = new();
    private readonly TimeProvider _timeProvider;

    public ProfileActivityLinkLookupThrottle(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>True, and counted, when the member may have another page read now.</summary>
    public bool TryAcquire(Guid memberKey)
    {
        var now = _timeProvider.GetUtcNow();
        var reads = _reads.GetOrAdd(memberKey, _ => new Queue<DateTimeOffset>());
        lock (reads)
        {
            while (reads.Count > 0 && now - reads.Peek() >= ProfileActivityLinkLookupService.LookupWindow)
            {
                reads.Dequeue();
            }

            if (reads.Count >= ProfileActivityLinkLookupService.MaxLookupsPerWindow)
            {
                return false;
            }

            reads.Enqueue(now);
            return true;
        }
    }
}

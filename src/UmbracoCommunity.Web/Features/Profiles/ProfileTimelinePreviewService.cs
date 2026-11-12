using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Gives timeline entries whose page offers a picture a preview card (picture, title, description). The picture is
/// the one already known (a video's thumbnail), otherwise the page's share image, read server side by
/// <see cref="LinkPreviewService"/> with all of its safeguards. What a page offered is kept in
/// <see cref="ProfileTimelinePreviewStore"/> and the picture itself is copied by <see cref="ProfileTimelineImageStore"/>
/// and served from this site, so a card keeps working when the page or picture later disappears. A page with no
/// picture, or one that fails, is remembered briefly. A render does the outside work (reading pages, copying
/// pictures) for at most <see cref="MaxFetchesPerRender"/> entries, a few at a time across all requests, and waits no
/// longer than <see cref="RenderBudget"/>; anything not ready by then is shown as a plain line, so a slow or failing
/// site never makes the profile slow or fail. Pictures are never loaded from the other site by the visitor's browser.
/// </summary>
public sealed class ProfileTimelinePreviewService
{
    /// <summary>How many entries one render may do outside work for.</summary>
    public const int MaxFetchesPerRender = 6;

    /// <summary>How many outside requests run at once, across all requests.</summary>
    private const int MaxConcurrentFetches = 4;

    private const string CacheKeyPrefix = "ProfileTimelinePreview:";

    /// <summary>The longest a render waits for outside work.</summary>
    public static readonly TimeSpan RenderBudget = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan AvailableCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly SemaphoreSlim FetchGate = new(MaxConcurrentFetches, MaxConcurrentFetches);

    private readonly LinkPreviewService _linkPreviewService;
    private readonly ProfileTimelinePreviewStore _previewStore;
    private readonly ProfileTimelineImageStore _imageStore;
    private readonly AppCaches _appCaches;
    private readonly ILogger<ProfileTimelinePreviewService> _logger;

    public ProfileTimelinePreviewService(
        LinkPreviewService linkPreviewService,
        ProfileTimelinePreviewStore previewStore,
        ProfileTimelineImageStore imageStore,
        AppCaches appCaches,
        ILogger<ProfileTimelinePreviewService> logger)
    {
        _linkPreviewService = linkPreviewService;
        _previewStore = previewStore;
        _imageStore = imageStore;
        _appCaches = appCaches;
        _logger = logger;
    }

    /// <summary>
    /// True when the member wants <paramref name="entry"/> shown as a card: cards are on for the timeline, the entry
    /// isn't switched to a plain line on its own, and it links to a page.
    /// </summary>
    public static bool WantsCard(ProfileDisplayChoices choices, ProfileTimelineEntry entry) =>
        !choices.HideTimelineCards
        && entry.Url != null
        && (entry.ActivityId == null || !choices.NoCardActivityIds.Contains(entry.ActivityId, StringComparer.Ordinal));

    /// <summary>
    /// The card for an entry the member wants as a card, from our stored copy of its picture. Null when there is no
    /// picture, so the entry stays a plain line.
    /// </summary>
    public static ProfileTimelineCard? Card(ProfileTimelineEntry entry, string? storedImageUrl, string? pageDescription) =>
        string.IsNullOrEmpty(storedImageUrl)
            ? null
            : new ProfileTimelineCard(storedImageUrl, entry.Title, entry.Description ?? pageDescription);

    /// <summary>Returns <paramref name="entries"/> in the same order, each with its card when it has one.</summary>
    public async Task<IReadOnlyList<ProfileTimelineEntry>> AddCardsAsync(
        IReadOnlyList<ProfileTimelineEntry> entries,
        ProfileDisplayChoices choices,
        CancellationToken cancellationToken)
    {
        var wanted = choices.HideTimelineCards ? [] : entries.Where(e => WantsCard(choices, e)).ToList();
        if (wanted.Count == 0)
        {
            return entries;
        }

        // What each page offered: remembered in memory, otherwise kept in the database.
        var pages = new Dictionary<string, PagePreview>(StringComparer.Ordinal);
        var unknownPages = new List<string>();
        foreach (var url in wanted.Where(e => e.ImageUrl == null).Select(e => e.Url!).Distinct(StringComparer.Ordinal))
        {
            if (_appCaches.RuntimeCache.Get(CacheKeyPrefix + url) is PagePreview cached)
            {
                pages[url] = cached;
            }
            else
            {
                unknownPages.Add(url);
            }
        }

        foreach (var (url, kept) in await GetKeptAsync(unknownPages, cancellationToken))
        {
            pages[url] = Remember(url, new PagePreview(kept.ImageUrl, kept.Description));
        }

        // Each entry's card, where everything needed is already here; the rest need outside work.
        var cards = new Dictionary<ProfileTimelineEntry, ProfileTimelineCard?>(ReferenceEqualityComparer.Instance);
        var toFetch = new List<ProfileTimelineEntry>();
        foreach (var entry in wanted)
        {
            var page = entry.ImageUrl == null ? pages.GetValueOrDefault(entry.Url!) : null;
            if (entry.ImageUrl == null && page == null)
            {
                Queue(entry);
                continue;
            }

            var source = entry.ImageUrl ?? page!.ImageUrl;
            if (source == null)
            {
                cards[entry] = null;
                continue;
            }

            if (_imageStore.FindStored(source) is { } stored)
            {
                cards[entry] = Card(entry, stored, page?.Description);
            }
            else
            {
                Queue(entry);
            }
        }

        void Queue(ProfileTimelineEntry entry)
        {
            // Entries are newest first, so the newest get their cards first.
            if (toFetch.Count < MaxFetchesPerRender)
            {
                toFetch.Add(entry);
            }
        }

        if (toFetch.Count > 0)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(RenderBudget);
            var fetched = await Task.WhenAll(toFetch.Select(entry => ResolveAsync(entry, pages.GetValueOrDefault(entry.Url!), budget.Token)));
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (entry, card) in toFetch.Zip(fetched))
            {
                cards[entry] = card;
            }
        }

        return entries
            .Select(entry => cards.GetValueOrDefault(entry) is { } card ? entry with { Card = card } : entry)
            .ToList();
    }

    /// <summary>Reads the entry's page when needed and copies its picture. Null when there is no card by the end of the budget.</summary>
    private async Task<ProfileTimelineCard?> ResolveAsync(ProfileTimelineEntry entry, PagePreview? page, CancellationToken budget)
    {
        try
        {
            if (entry.ImageUrl == null && page == null)
            {
                page = await FetchPageAsync(entry.Url!, budget);
            }

            var source = entry.ImageUrl ?? page?.ImageUrl;
            if (source == null)
            {
                return null;
            }

            await FetchGate.WaitAsync(budget);
            try
            {
                return Card(entry, await _imageStore.GetOrStoreAsync(source, budget), page?.Description);
            }
            finally
            {
                FetchGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Not ready within this render; a later render carries on.
            return null;
        }
    }

    /// <summary>Reads what a page offers, remembers it, and keeps it when it has a picture.</summary>
    private async Task<PagePreview> FetchPageAsync(string url, CancellationToken budget)
    {
        await FetchGate.WaitAsync(budget);
        PagePreview result;
        try
        {
            var preview = ProfileLinkUrl.TryParse(url, out _)
                ? await _linkPreviewService.FetchAsync(url, budget)
                : LinkPreview.Unreachable;
            result = preview.Status == LinkPreviewStatus.Available
                ? new PagePreview(preview.ImageUrl, preview.Description)
                : PagePreview.Nothing;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "No timeline preview for a link on {Host}.", new Uri(url).Host);
            result = PagePreview.Nothing;
        }
        catch (OperationCanceledException)
        {
            // Too slow for this render: shown as a plain line, and not tried again for a while.
            Remember(url, PagePreview.Nothing);
            throw;
        }
        finally
        {
            FetchGate.Release();
        }

        if (result.ImageUrl != null)
        {
            try
            {
                await _previewStore.SaveAsync(url, result.ImageUrl, result.Description, budget);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not keep the timeline preview for a link on {Host}.", new Uri(url).Host);
            }
        }

        return Remember(url, result);
    }

    private async Task<IReadOnlyDictionary<string, Data.Entities.ProfileTimelinePreviewEntity>> GetKeptAsync(
        IReadOnlyList<string> pageUrls,
        CancellationToken cancellationToken)
    {
        if (pageUrls.Count == 0)
        {
            return new Dictionary<string, Data.Entities.ProfileTimelinePreviewEntity>();
        }

        try
        {
            return await _previewStore.GetAsync(pageUrls, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not load the kept timeline previews");
            return new Dictionary<string, Data.Entities.ProfileTimelinePreviewEntity>();
        }
    }

    private PagePreview Remember(string url, PagePreview preview)
    {
        var duration = preview.ImageUrl != null ? AvailableCacheDuration : FailureCacheDuration;
        _appCaches.RuntimeCache.Insert(CacheKeyPrefix + url, () => preview, duration);
        return preview;
    }

    /// <summary>What a page offers for a card. The image is the page's own https address; it is copied before it is shown.</summary>
    private sealed record PagePreview(string? ImageUrl, string? Description)
    {
        public static PagePreview Nothing { get; } = new(null, null);
    }
}

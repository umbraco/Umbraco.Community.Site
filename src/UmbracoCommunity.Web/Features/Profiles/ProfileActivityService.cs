using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Matching;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Fills the activity sections of a <see cref="CommunityProfile"/> (blog posts, videos, forum posts, packages,
/// social posts, meet-ups) from the external content platform, newest first. The platform profile is found by
/// the member's GitHub handle only. The result is cached per handle; if the platform has nothing, is not
/// configured or fails, the profile is returned unchanged. Platform activity from a source that was rejected for the
/// member (see <see cref="ProfileMatchStore"/>) is left out; that is checked on every call, outside the cache, so a
/// rejection takes effect at once. Activity the member added by hand and an admin verified
/// (see <see cref="ProfileActivityEntryService"/>) is added to the matching sections.
/// </summary>
public sealed class ProfileActivityService
{
    private const string CacheKeyPrefix = "ProfileActivity:";
    private const int MaxTextLength = 280;

    private readonly IProfileSyncClient _syncClient;
    private readonly ProfileActivityEntryService _activityEntryService;
    private readonly ProfileMatchStore _matchStore;
    private readonly IMvpDataService _mvpDataService;
    private readonly AppCaches _appCaches;
    private readonly IOptionsMonitor<ProfileActivityOptions> _options;
    private readonly ILogger<ProfileActivityService> _logger;

    public ProfileActivityService(
        IProfileSyncClient syncClient,
        ProfileActivityEntryService activityEntryService,
        ProfileMatchStore matchStore,
        IMvpDataService mvpDataService,
        AppCaches appCaches,
        IOptionsMonitor<ProfileActivityOptions> options,
        ILogger<ProfileActivityService> logger)
    {
        _syncClient = syncClient;
        _activityEntryService = activityEntryService;
        _matchStore = matchStore;
        _mvpDataService = mvpDataService;
        _appCaches = appCaches;
        _options = options;
        _logger = logger;
    }

    public async Task<CommunityProfile> EnrichAsync(CommunityProfile profile, CancellationToken cancellationToken = default)
    {
        var activity = await GetActivityAsync(profile.Slug, cancellationToken);
        var identity = ProfileMvp.Apply(profile.Identity, activity?.MvpYears, _mvpDataService.GetAll());
        var enriched = ReferenceEquals(identity, profile.Identity) ? profile : profile with { Identity = identity };

        if (activity != null)
        {
            enriched = enriched with
            {
                BlogPosts = activity.BlogPosts,
                Videos = activity.Videos,
                ForumPosts = activity.ForumPosts,
                Packages = activity.Packages,
                SocialPosts = activity.SocialPosts,
                Meetups = activity.Meetups,
            };
            enriched = WithoutRejectedSources(enriched, await GetRejectedSourcesAsync(profile.Slug, cancellationToken));
        }

        return AddVerifiedActivity(enriched, await GetVerifiedActivityAsync(profile.Slug, cancellationToken));
    }

    /// <summary>
    /// The MVP years the external content platform reports for <paramref name="handle"/>, from the same per-handle cache
    /// <see cref="EnrichAsync"/> uses. Null when the platform has no profile for the handle, is not configured or fails;
    /// pass the result to <see cref="ProfileMvp.Resolve"/>, which then falls back to the MVP list.
    /// </summary>
    public async Task<IReadOnlyList<int>?> GetPlatformMvpYearsAsync(string handle, CancellationToken cancellationToken = default) =>
        (await GetActivityAsync(handle, cancellationToken))?.MvpYears;

    private async Task<IReadOnlyList<ActivityEntryRecord>> GetVerifiedActivityAsync(string handle, CancellationToken cancellationToken)
    {
        try
        {
            return await _activityEntryService.GetVerifiedAsync(handle, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not load the activity {Handle} added by hand", handle);
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> GetRejectedSourcesAsync(string handle, CancellationToken cancellationToken)
    {
        try
        {
            return await _matchStore.GetRejectedSourcesAsync(handle, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not load the rejected sources of {Handle}; showing all platform activity", handle);
            return [];
        }
    }

    /// <summary>
    /// Leaves out the items in the platform activity sections whose link comes from one of
    /// <paramref name="rejectedSources"/> (see <see cref="ProfileLinkMatchingService.SourceCovers"/>).
    /// </summary>
    internal static CommunityProfile WithoutRejectedSources(CommunityProfile profile, IReadOnlyList<string> rejectedSources)
    {
        if (rejectedSources.Count == 0)
        {
            return profile;
        }

        bool Keep(string? url) => url == null || !rejectedSources.Any(source => ProfileLinkMatchingService.SourceCovers(source, url));

        return profile with
        {
            BlogPosts = profile.BlogPosts.Where(p => Keep(p.Url)).ToList(),
            Videos = profile.Videos.Where(v => Keep(v.Url)).ToList(),
            ForumPosts = profile.ForumPosts.Where(p => Keep(p.Url)).ToList(),
            Packages = profile.Packages.Where(p => Keep(p.Url)).ToList(),
            SocialPosts = profile.SocialPosts.Where(p => Keep(p.Url)).ToList(),
            Meetups = profile.Meetups.Where(m => Keep(m.Url)).ToList(),
        };
    }

    /// <summary>
    /// Adds verified hand-added activity to the section its kind belongs to, newest first. A link the platform already
    /// reports for the member is not shown twice.
    /// </summary>
    internal static CommunityProfile AddVerifiedActivity(CommunityProfile profile, IReadOnlyList<ActivityEntryRecord> entries)
    {
        if (entries.Count == 0)
        {
            return profile;
        }

        DateTimeOffset At(ActivityEntryRecord entry) => new(entry.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        IEnumerable<ActivityEntryRecord> InSection(string group, IEnumerable<string> existingUrls) =>
            entries.Where(e => ProfileActivityEntry.Group(e.Kind) == group
                && !existingUrls.Contains(e.Url, StringComparer.OrdinalIgnoreCase));

        return profile with
        {
            BlogPosts = profile.BlogPosts
                .Concat(InSection(PlatformActivityGroups.Blog, profile.BlogPosts.Select(p => p.Url))
                    .Select(e => new ProfileBlogPost(e.Title, e.Url, At(e), null, e.Description, ProfileActivityEntry.ActivityId(e.Id))))
                .OrderByDescending(p => p.PublishedAt)
                .ToList(),
            Videos = profile.Videos
                .Concat(InSection(PlatformActivityGroups.Videos, profile.Videos.Select(v => v.Url))
                    .Select(e => new ProfileVideo(e.Title, e.Url, null, At(e), e.Description, ProfileActivityEntry.ActivityId(e.Id))))
                .OrderByDescending(v => v.PublishedAt)
                .ToList(),
            Packages = profile.Packages
                .Concat(InSection(PlatformActivityGroups.Packages, profile.Packages.Select(p => p.Url))
                    .Select(e => new ProfilePackage(e.Title, e.Url, [], e.Description, ProfileActivityEntry.ActivityId(e.Id), At(e))))
                .ToList(),
            Meetups = profile.Meetups
                .Concat(InSection(PlatformActivityGroups.Meetups, profile.Meetups.Select(m => m.Url ?? string.Empty))
                    .Select(e => new ProfileMeetup("Took part in", e.Title, null, At(e), e.Url, ProfileActivityEntry.ActivityId(e.Id))))
                .OrderByDescending(m => m.Date)
                .ToList(),
            OtherActivities = profile.OtherActivities
                .Concat(InSection(PlatformActivityGroups.Other, [])
                    .Select(e => new ProfileOtherActivity(ProfileActivityEntry.KindLabel(e.Kind), e.Title, e.Url, At(e), e.Description, ProfileActivityEntry.ActivityId(e.Id))))
                .OrderByDescending(a => a.Date)
                .ToList(),
        };
    }

    private async Task<ProfileActivity?> GetActivityAsync(string handle, CancellationToken cancellationToken)
    {
        var key = CacheKeyPrefix + handle.ToLowerInvariant();
        if (_appCaches.RuntimeCache.Get(key) is CachedActivity cached)
        {
            return cached.Activity;
        }

        CachedActivity result;
        TimeSpan ttl;
        var options = _options.CurrentValue;
        try
        {
            result = new CachedActivity(await FetchAsync(handle, Math.Max(1, options.ItemsPerSection), cancellationToken));
            ttl = TimeSpan.FromMinutes(Math.Max(1, options.CacheMinutes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not load activity for {Handle} from the content platform", handle);
            result = new CachedActivity(null);
            ttl = TimeSpan.FromMinutes(Math.Max(1, options.FailureCacheMinutes));
        }

        _appCaches.RuntimeCache.Insert(key, () => result, ttl);
        return result.Activity;
    }

    private async Task<ProfileActivity?> FetchAsync(string handle, int limit, CancellationToken cancellationToken)
    {
        var platformProfile = await _syncClient.FindProfileAsync(handle, cancellationToken);
        if (platformProfile == null)
        {
            return null;
        }

        var groups = await Task.WhenAll(PlatformActivityGroups.FromPlatform.Select(async group =>
            (Group: group, Items: await _syncClient.GetActivitiesAsync(platformProfile.Id, group, limit, cancellationToken))));
        IReadOnlyList<PlatformActivity> Items(string group) =>
            groups.First(g => g.Group == group).Items
                .Where(a => !string.IsNullOrWhiteSpace(a.Title) && IsWebUrl(a.Url))
                .OrderByDescending(a => a.OccurredAt)
                .Take(limit)
                .ToList();

        return new ProfileActivity(
            MvpYears: platformProfile.MvpYears,
            BlogPosts: Items(PlatformActivityGroups.Blog)
                .Select(a => new ProfileBlogPost(a.Title!, a.Url!, a.OccurredAt, a.Platform, Truncate(a.Summary), a.Id))
                .ToList(),
            Videos: Items(PlatformActivityGroups.Videos)
                .Select(a => new ProfileVideo(a.Title!, a.Url!, a.ImageUrl, a.OccurredAt, Truncate(a.Summary), a.Id))
                .ToList(),
            ForumPosts: Items(PlatformActivityGroups.Forum)
                .Select(a => new ProfileForumPost(a.Title!, a.Url!, a.OccurredAt, null, a.Id))
                .ToList(),
            Packages: Items(PlatformActivityGroups.Packages)
                .DistinctBy(a => a.Url)
                .Select(a => new ProfilePackage(a.Title!, a.Url!, [], Truncate(a.Summary), a.Id, a.OccurredAt))
                .ToList(),
            SocialPosts: Items(PlatformActivityGroups.Social)
                .Select(a => new ProfileSocialPost(a.Platform ?? "Social", a.Url!, Truncate(a.Title)!, a.OccurredAt, a.Id))
                .ToList(),
            Meetups: Items(PlatformActivityGroups.Meetups)
                .Select(a => new ProfileMeetup(MeetupAction(a.Type), a.Title!, null, a.OccurredAt, a.Url, a.Id))
                .ToList());
    }

    /// <summary>Attended events read as attended; anything else is an RSVP.</summary>
    internal static string MeetupAction(string type) =>
        type.Contains("attend", StringComparison.OrdinalIgnoreCase) ? "Attended" : "RSVP'd yes to";

    // Platform text is shown as a link target, so only plain web addresses are accepted.
    private static bool IsWebUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string? Truncate(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxTextLength ? trimmed : trimmed[..MaxTextLength].TrimEnd() + "…";
    }

    private sealed record ProfileActivity(
        IReadOnlyList<int> MvpYears,
        IReadOnlyList<ProfileBlogPost> BlogPosts,
        IReadOnlyList<ProfileVideo> Videos,
        IReadOnlyList<ProfileForumPost> ForumPosts,
        IReadOnlyList<ProfilePackage> Packages,
        IReadOnlyList<ProfileSocialPost> SocialPosts,
        IReadOnlyList<ProfileMeetup> Meetups);

    // Wraps the (possibly null) answer so "nothing found" is cached too.
    private sealed record CachedActivity(ProfileActivity? Activity);
}

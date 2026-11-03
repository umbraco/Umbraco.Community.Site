using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Fills the activity sections of a <see cref="CommunityProfile"/> (blog posts, videos, forum posts, packages,
/// social posts, meet-ups) from the external content platform, newest first. The platform profile is found by
/// the member's GitHub handle only. The result is cached per handle; if the platform has nothing, is not
/// configured or fails, the profile is returned unchanged.
/// </summary>
public sealed class ProfileActivityService
{
    private const string CacheKeyPrefix = "ProfileActivity:";
    private const int MaxTextLength = 280;

    private readonly IProfileSyncClient _syncClient;
    private readonly FeedSubmissionImageProxyService _imageProxy;
    private readonly AppCaches _appCaches;
    private readonly IOptionsMonitor<ProfileActivityOptions> _options;
    private readonly ILogger<ProfileActivityService> _logger;

    public ProfileActivityService(
        IProfileSyncClient syncClient,
        FeedSubmissionImageProxyService imageProxy,
        AppCaches appCaches,
        IOptionsMonitor<ProfileActivityOptions> options,
        ILogger<ProfileActivityService> logger)
    {
        _syncClient = syncClient;
        _imageProxy = imageProxy;
        _appCaches = appCaches;
        _options = options;
        _logger = logger;
    }

    public async Task<CommunityProfile> EnrichAsync(CommunityProfile profile, CancellationToken cancellationToken = default)
    {
        var activity = await GetActivityAsync(profile.Slug, cancellationToken);
        return activity == null
            ? profile
            : profile with
            {
                BlogPosts = activity.BlogPosts,
                Videos = activity.Videos,
                ForumPosts = activity.ForumPosts,
                Packages = activity.Packages,
                SocialPosts = activity.SocialPosts,
                Meetups = activity.Meetups,
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

        var groups = await Task.WhenAll(PlatformActivityGroups.All.Select(async group =>
            (Group: group, Items: await _syncClient.GetActivitiesAsync(platformProfile.Id, group, limit, cancellationToken))));
        IReadOnlyList<PlatformActivity> Items(string group) =>
            groups.First(g => g.Group == group).Items
                .Where(a => !string.IsNullOrWhiteSpace(a.Title) && IsWebUrl(a.Url))
                .OrderByDescending(a => a.OccurredAt)
                .Take(limit)
                .ToList();

        return new ProfileActivity(
            BlogPosts: Items(PlatformActivityGroups.Blog)
                .Select(a => new ProfileBlogPost(a.Title!, a.Url!, a.OccurredAt, a.Platform, Truncate(a.Summary)))
                .ToList(),
            Videos: Items(PlatformActivityGroups.Videos)
                .Select(a => new ProfileVideo(a.Title!, a.Url!, _imageProxy.CreateProxyUrl(a.ImageUrl), a.OccurredAt, Truncate(a.Summary)))
                .ToList(),
            ForumPosts: Items(PlatformActivityGroups.Forum)
                .Select(a => new ProfileForumPost(a.Title!, a.Url!, a.OccurredAt, null))
                .ToList(),
            Packages: Items(PlatformActivityGroups.Packages)
                .DistinctBy(a => a.Url)
                .Select(a => new ProfilePackage(a.Title!, a.Url!, [], Truncate(a.Summary)))
                .ToList(),
            SocialPosts: Items(PlatformActivityGroups.Social)
                .Select(a => new ProfileSocialPost(a.Platform ?? "Social", a.Url!, Truncate(a.Title)!, a.OccurredAt))
                .ToList(),
            Meetups: Items(PlatformActivityGroups.Meetups)
                .Select(a => new ProfileMeetup(MeetupAction(a.Type), a.Title!, null, a.OccurredAt, a.Url))
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
        IReadOnlyList<ProfileBlogPost> BlogPosts,
        IReadOnlyList<ProfileVideo> Videos,
        IReadOnlyList<ProfileForumPost> ForumPosts,
        IReadOnlyList<ProfilePackage> Packages,
        IReadOnlyList<ProfileSocialPost> SocialPosts,
        IReadOnlyList<ProfileMeetup> Meetups);

    // Wraps the (possibly null) answer so "nothing found" is cached too.
    private sealed record CachedActivity(ProfileActivity? Activity);
}

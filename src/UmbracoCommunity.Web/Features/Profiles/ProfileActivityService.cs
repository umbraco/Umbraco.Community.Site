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
    private readonly IPackageFirstVersionLookup? _firstVersions;
    private readonly ICrossPostJudge? _crossPostJudge;

    public ProfileActivityService(
        IProfileSyncClient syncClient,
        ProfileActivityEntryService activityEntryService,
        ProfileMatchStore matchStore,
        IMvpDataService mvpDataService,
        AppCaches appCaches,
        IOptionsMonitor<ProfileActivityOptions> options,
        ILogger<ProfileActivityService> logger,
        IPackageFirstVersionLookup? firstVersions = null,
        ICrossPostJudge? crossPostJudge = null)
    {
        _firstVersions = firstVersions;
        _crossPostJudge = crossPostJudge;
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
                ForumStats = activity.ForumStats,
                ActivityCounts = activity.ActivityCounts,
                Packages = activity.Packages,
                SocialPosts = activity.SocialPosts,
                SamePosts = activity.SamePosts,
                JudgingProgress = activity.JudgingProgress,
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

    /// <summary>How long a fetched activity is kept: briefly while questions are left or the model is down, so the next visit carries on.</summary>
    private static TimeSpan ActivityTtl(ProfileActivity? activity, ProfileActivityOptions options) =>
        TimeSpan.FromMinutes(Math.Max(1, activity switch
        {
            { JudgingProgress: not null } => 5,
            { RetrySoon: true } => options.FailureCacheMinutes,
            _ => options.CacheMinutes,
        }));

    private async Task<ProfileActivity?> GetActivityAsync(string handle, CancellationToken cancellationToken)
    {
        var key = CacheKeyPrefix + handle.ToLowerInvariant();
        if (_appCaches.RuntimeCache.Get(key) is CachedActivity cached)
        {
            if (cached.Activity is not { JudgingProgress: not null } pending)
            {
                return cached.Activity;
            }

            // Still building: every visit asks the next batch of questions, until none are left.
            var updated = await ApplyJudgmentAsync(pending, cancellationToken);
            _appCaches.RuntimeCache.Insert(key, () => new CachedActivity(updated), ActivityTtl(updated, _options.CurrentValue));
            return updated;
        }

        CachedActivity result;
        TimeSpan ttl;
        var options = _options.CurrentValue;
        try
        {
            result = new CachedActivity(await FetchAsync(handle, Math.Max(1, options.ItemsPerSection), cancellationToken));
            ttl = ActivityTtl(result.Activity, options);
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

        // The exact forum counts come from their own endpoint. Losing them must not lose the rest of the profile's activity.
        var forumStatsTask = GetForumStatsAsync(platformProfile.Id, handle, cancellationToken);

        // Each section's newest items, to show; and, to count, everything in the last 12 months (see CountWindowAsync).
        var windowStart = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-12).AddDays(1);
        var platformGroups = _options.CurrentValue.IncludeLinkedIn
            ? [.. PlatformActivityGroups.FromPlatform, LinkedInGroup]
            : PlatformActivityGroups.FromPlatform;
        var fetched = await Task.WhenAll(platformGroups.Select(async group =>
        {
            var newest = await _syncClient.GetActivitiesAsync(platformProfile.Id, group, limit, cancellationToken);
            var window = await GetWindowAsync(platformProfile.Id, group, newest, limit, windowStart, handle, cancellationToken);
            return (Group: group, Newest: newest, Window: window);
        }));

        // LinkedIn posts are social posts: their text is the post (they have no title), and they join the social section.
        var groups = fetched.Where(g => g.Group != LinkedInGroup).ToArray();
        if (fetched.FirstOrDefault(g => g.Group == LinkedInGroup) is { Group: not null } linkedIn)
        {
            IReadOnlyList<PlatformActivity> AsSocial(IReadOnlyList<PlatformActivity> posts) =>
                posts.Select(a => string.IsNullOrWhiteSpace(a.Title) ? a with { Title = a.Summary, Platform = a.Platform ?? "linkedin" } : a).ToList();

            var socialIndex = Array.FindIndex(groups, g => g.Group == PlatformActivityGroups.Social);
            groups[socialIndex] = (
                PlatformActivityGroups.Social,
                groups[socialIndex].Newest.Concat(AsSocial(linkedIn.Newest)).ToList(),
                groups[socialIndex].Window.Concat(AsSocial(linkedIn.Window)).ToList());
        }

        IReadOnlyList<PlatformActivity> Newest(string group) => Usable(groups.First(g => g.Group == group).Newest, limit);
        IReadOnlyList<PlatformActivity> Window(string group) => Usable(groups.First(g => g.Group == group).Window, int.MaxValue);

        // What the profile holds: everything from the last 12 months, plus the newest few of a quiet section, which may
        // be older (so a profile with little recent activity still has something to show).
        IReadOnlyList<PlatformActivity> Held(string group) =>
            Newest(group).Concat(Window(group)).DistinctBy(a => a.Id).OrderByDescending(a => a.OccurredAt).ToList();

        var forumStats = await forumStatsTask;
        var firstReleases = await FindFirstReleasesAsync(Held(PlatformActivityGroups.Packages), cancellationToken);

        var shown = BuildActivity(platformProfile, Held, firstReleases);
        var counted = BuildActivity(platformProfile, Window, firstReleases);
        var activityCounts = new Dictionary<string, int>
        {
            [PlatformActivityGroups.Blog] = counted.BlogPosts.Count,
            [PlatformActivityGroups.Videos] = counted.Videos.Count,
            // Every release counts, not just each package once: updates released is the figure of interest.
            [PlatformActivityGroups.Packages] = counted.Packages.Count,
            // Topics and accepted answers are both listed in the forum section; the forum counts cover calendar months,
            // which would drop answers from the first weeks of the rolling window.
            [PlatformActivityGroups.Forum] = counted.ForumPosts.Count(p => !p.IsSolution),
            [ProfileActivityGraph.SolvedKey] = counted.ForumPosts.Count(p => p.IsSolution),
            [PlatformActivityGroups.Meetups] = counted.Meetups.Count,
        };

        var activity = shown with
        {
            ForumStats = forumStats,
            ActivityCounts = activityCounts,
            SocialToJudge = shown.SocialPosts.Concat(counted.SocialPosts).DistinctBy(p => p.ActivityId ?? p.Url).ToList(),
            CountedSocialPosts = counted.SocialPosts,
        };
        return await ApplyJudgmentAsync(activity, cancellationToken);
    }

    /// <summary>
    /// Asks the model about the social post pairs still waiting (a batch at a time; answers already kept cost nothing),
    /// and updates what depends on the answers: the merged pairs, the social count, and the progress while pairs are left.
    /// </summary>
    private async Task<ProfileActivity> ApplyJudgmentAsync(ProfileActivity activity, CancellationToken cancellationToken)
    {
        var judgment = await FindSamePostsAsync(activity.SocialToJudge, cancellationToken);
        var counts = new Dictionary<string, int>(activity.ActivityCounts ?? new Dictionary<string, int>())
        {
            // A post shared more than once counts once, as on the timeline.
            [PlatformActivityGroups.Social] = ProfileTimeline.MergeCrossPosts(activity.CountedSocialPosts.Select(p => new ProfileTimelineEntry(
                PlatformActivityGroups.Social, p.ActivityId, p.Text, p.Url, null, p.PostedAt, p.Platform, null)), judgment.Same).Count,
        };

        return activity with
        {
            SamePosts = judgment.Same,
            ActivityCounts = counts,
            JudgingProgress = judgment.HasMoreToAsk ? new ProfileJudgingProgress(judgment.Answered, judgment.Total) : null,
            RetrySoon = judgment.ModelUnavailable,
        };
    }

    /// <summary>
    /// The pairs of social posts a model says are the same post shared twice. Empty when the check is off,
    /// there is no judge or model, or no posts could match; never throws, so a model problem can't lose the profile.
    /// </summary>
    private async Task<CrossPostJudgment> FindSamePostsAsync(IReadOnlyList<ProfileSocialPost> posts, CancellationToken cancellationToken)
    {
        if (_crossPostJudge is null || !_options.CurrentValue.AiCrossPostCheck)
        {
            return CrossPostJudgment.None;
        }

        try
        {
            var pairs = ProfileTimeline.CandidatePairs(posts.Select(p => new ProfileTimelineEntry(
                PlatformActivityGroups.Social, p.ActivityId, p.Text, p.Url, null, p.PostedAt, p.Platform, null)));
            return await _crossPostJudge.JudgeAsync(pairs, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not check for cross-posted social posts");
            return new CrossPostJudgment(new HashSet<string>(), 0, 0, ModelUnavailable: true);
        }
    }

    /// <summary>The platform's opt-in group for LinkedIn posts (not part of <see cref="PlatformActivityGroups.FromPlatform"/>).</summary>
    private const string LinkedInGroup = "linkedin";

    /// <summary>The most items counted per section; a member with more is counted as having this many.</summary>
    private const int MaxCountedPerSection = 500;

    /// <summary>
    /// Everything in a section since <paramref name="windowStart"/>. The newest items already fetched are the whole
    /// answer unless they might have been cut off (as many as asked for, the oldest still inside the window); then the
    /// rest is fetched. Failing to count must not lose the items: it falls back to what was fetched.
    /// </summary>
    private async Task<IReadOnlyList<PlatformActivity>> GetWindowAsync(
        string platformProfileId,
        string group,
        IReadOnlyList<PlatformActivity> newest,
        int limit,
        DateOnly windowStart,
        string handle,
        CancellationToken cancellationToken)
    {
        bool InWindow(PlatformActivity a) => DateOnly.FromDateTime(a.OccurredAt.UtcDateTime) >= windowStart;
        var inWindow = newest.Where(InWindow).ToList();
        if (newest.Count < limit || inWindow.Count < newest.Count)
        {
            return inWindow;
        }

        try
        {
            return await _syncClient.GetActivitiesSinceAsync(platformProfileId, group, windowStart, MaxCountedPerSection, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not count the {Group} activity of {Handle} from the content platform", group, handle);
            return inWindow;
        }
    }

    private static IReadOnlyList<PlatformActivity> Usable(IReadOnlyList<PlatformActivity> items, int limit) =>
        items
            .Where(a => !string.IsNullOrWhiteSpace(a.Title) && IsWebUrl(a.Url))
            .OrderByDescending(a => a.OccurredAt)
            .Take(limit)
            .ToList();

    private static ProfileActivity BuildActivity(
        PlatformProfile platformProfile, Func<string, IReadOnlyList<PlatformActivity>> items, HashSet<string> firstReleases) =>
        new(
            MvpYears: platformProfile.MvpYears,
            BlogPosts: items(PlatformActivityGroups.Blog)
                .Select(a => new ProfileBlogPost(a.Title!, a.Url!, a.OccurredAt, a.Platform, Truncate(a.Summary), a.Id))
                .ToList(),
            Videos: items(PlatformActivityGroups.Videos)
                .Select(a => new ProfileVideo(a.Title!, a.Url!, a.ImageUrl, a.OccurredAt, Truncate(a.Summary), a.Id))
                .ToList(),
            ForumPosts: items(PlatformActivityGroups.Forum)
                .Select(a => new ProfileForumPost(a.Title!, a.Url!, a.OccurredAt, null, a.Id, IsSolution(a.Type)))
                .ToList(),
            Packages: items(PlatformActivityGroups.Packages)
                .DistinctBy(a => a.Url)
                .Select(a => new ProfilePackage(a.Title!, a.Url!, [], Truncate(a.Summary), a.Id, a.OccurredAt, firstReleases.Contains(a.Id)))
                .ToList(),
            SocialPosts: items(PlatformActivityGroups.Social)
                .Select(a => new ProfileSocialPost(a.Platform ?? "Social", a.Url!, Truncate(a.Title)!, a.OccurredAt, a.Id))
                .ToList(),
            // An RSVP, an attendance, hosting and speaking at the same event are one item, shown as the most
            // notable of them (speaking, then hosting, then signing up). Items without a link can't be matched.
            Meetups: items(PlatformActivityGroups.Meetups)
                .GroupBy(a => a.Url)
                .SelectMany(g => g.Key == null
                    ? g.AsEnumerable()
                    : new[] { g.OrderByDescending(a => MeetupRank(a.Type)).ThenByDescending(a => a.OccurredAt).First() })
                .OrderByDescending(a => a.OccurredAt)
                .Select(a => new ProfileMeetup(MeetupAction(a.Type), a.Title!, null, a.OccurredAt, a.Url, a.Id))
                .ToList());

    /// <summary>The most packages asked NuGet about for one profile (the newest releases first), and how many at once.</summary>
    private const int MaxPackagesLookedUp = 40;

    private const int MaxConcurrentLookups = 4;

    /// <summary>
    /// The ids of the release activities that are a package's very first release: the version released is the first
    /// version NuGet lists for the package. A package NuGet can't be asked about, or that can't be reached, is
    /// treated as a routine release.
    /// </summary>
    private async Task<HashSet<string>> FindFirstReleasesAsync(IReadOnlyList<PlatformActivity> releases, CancellationToken cancellationToken)
    {
        var firstReleases = new HashSet<string>(StringComparer.Ordinal);
        if (_firstVersions == null)
        {
            return firstReleases;
        }

        var byPackage = releases
            .Where(r => PackageReleases.IdOf(r.Url) != null && PackageReleases.VersionOf(r.Url) != null)
            .GroupBy(r => PackageReleases.IdOf(r.Url)!)
            .Take(MaxPackagesLookedUp)
            .ToList();
        using var gate = new SemaphoreSlim(MaxConcurrentLookups);
        var firstVersions = await Task.WhenAll(byPackage.Select(async g =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return (Releases: g, First: await _firstVersions.GetFirstVersionAsync(g.Key, cancellationToken));
            }
            finally
            {
                gate.Release();
            }
        }));

        foreach (var (group, first) in firstVersions)
        {
            foreach (var release in group.Where(r => PackageReleases.SameVersion(PackageReleases.VersionOf(r.Url), first)))
            {
                firstReleases.Add(release.Id);
            }
        }

        return firstReleases;
    }

    /// <summary>The months the forum counts cover; matches the 12-month activity graph.</summary>
    private const int ForumStatsMonths = 12;

    private async Task<PlatformForumStats?> GetForumStatsAsync(string platformProfileId, string handle, CancellationToken cancellationToken)
    {
        try
        {
            return await _syncClient.GetForumStatsAsync(platformProfileId, ForumStatsMonths, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not load the forum counts of {Handle} from the content platform", handle);
            return null;
        }
    }

    /// <summary>An accepted answer (<c>discourse:solution:accepted</c>) rather than a topic the member started.</summary>
    private static bool IsSolution(string type) => type.Contains("solution", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What the member did at a meet-up: speaking and hosting are their own, and everything else (an RSVP or a
    /// confirmed attendance) reads as signed up, since attendance can't be verified.
    /// </summary>
    internal static string MeetupAction(string type) => MeetupRank(type) switch
    {
        MeetupSpoke => "Spoke at",
        MeetupHosted => "Hosted",
        _ => "Signed up",
    };

    private const int MeetupSpoke = 2;
    private const int MeetupHosted = 1;

    /// <summary>How notable a meet-up activity is: speaking above hosting above signing up (0).</summary>
    private static int MeetupRank(string type) =>
        type.Contains("spoke", StringComparison.OrdinalIgnoreCase) ? MeetupSpoke
        : type.Contains("hosted", StringComparison.OrdinalIgnoreCase) ? MeetupHosted
        : 0;

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
        IReadOnlyList<ProfileMeetup> Meetups)
    {
        public PlatformForumStats? ForumStats { get; init; }

        public IReadOnlyDictionary<string, int>? ActivityCounts { get; init; }

        public IReadOnlySet<string> SamePosts { get; init; } = new HashSet<string>();

        /// <summary>The social posts the model is asked about (those shown and those counted).</summary>
        public IReadOnlyList<ProfileSocialPost> SocialToJudge { get; init; } = [];

        /// <summary>The social posts in the 12-month window, for the social count.</summary>
        public IReadOnlyList<ProfileSocialPost> CountedSocialPosts { get; init; } = [];

        /// <summary>Set while some pairs of social posts still wait for the model's answer; null once there are none left.</summary>
        public ProfileJudgingProgress? JudgingProgress { get; init; }

        /// <summary>True when the model could not be reached, so the result is kept only briefly and tried again.</summary>
        public bool RetrySoon { get; init; }
    }

    // Wraps the (possibly null) answer so "nothing found" is cached too.
    private sealed record CachedActivity(ProfileActivity? Activity);
}

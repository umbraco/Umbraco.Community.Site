using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles.Matching;

/// <summary>
/// Decides whether the links on a member's profile belong to them, and lets the member say a source is not theirs.
/// <list type="bullet">
/// <item>The member's own GitHub profile is confirmed: they signed in with it.</item>
/// <item>Any other link is confirmed only when the external content platform already ties the same account to the
/// same profile. A link it ties to a different profile, or knows nothing about, is held for review in the match
/// review dashboard. A link it cannot check is not recorded.</item>
/// </list>
/// Decisions are kept in <see cref="ProfileMatchStore"/>; a decision a person made is never replaced by a later check.
/// When the platform fails or is not configured nothing is recorded, and nothing here throws to the caller.
/// </summary>
public sealed class ProfileLinkMatchingService
{
    private static readonly MatchSignal GitHubSignal = new("GitHub handle", "The member's own GitHub profile");
    private static readonly MatchSignal ConfirmedSignal = new("Content platform", "Platform already links this account");
    private static readonly MatchSignal ConflictSignal = new("Content platform", "Points to an account linked to someone else");
    private static readonly MatchSignal UnknownSignal = new("Content platform", "Nothing known about this link");
    private static readonly MatchSignal MemberRejectionSignal = new("Member", "Member said this is not theirs");

    private static readonly string[] FeedFileNames = ["rss", "atom", "feed"];
    private static readonly string[] FeedExtensions = [".xml", ".rss", ".atom"];

    private readonly IProfileSyncClient _syncClient;
    private readonly ProfileMatchStore _matchStore;
    private readonly IOptionsMonitor<ProfileMatchingOptions> _options;
    private readonly ILogger<ProfileLinkMatchingService> _logger;

    public ProfileLinkMatchingService(
        IProfileSyncClient syncClient,
        ProfileMatchStore matchStore,
        IOptionsMonitor<ProfileMatchingOptions> options,
        ILogger<ProfileLinkMatchingService> logger)
    {
        _syncClient = syncClient;
        _matchStore = matchStore;
        _options = options;
        _logger = logger;
    }

    /// <summary>Checks the given links of a member's profile and records a decision for each one that can be decided.</summary>
    public async Task CheckLinksAsync(int memberProfileId, string handle, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = _options.CurrentValue;
            var links = urls
                .Select(u => u.Trim())
                .Where(u => ProfileLinkUrl.TryParse(u, out _) && u.Length <= ProfileMatchEntity.MaxSourceUrlLength)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var toVerify = new List<string>();
            foreach (var url in links)
            {
                if (IsOwnGitHubProfile(url, handle))
                {
                    await _matchStore.RecordAutomaticAsync(
                        memberProfileId, url, ProfileMatchState.Confirmed, options.ConfirmedConfidence, [GitHubSignal], cancellationToken);
                }
                else
                {
                    toVerify.Add(url);
                }
            }

            if (toVerify.Count == 0)
            {
                return;
            }

            var checks = await _syncClient.VerifyLinksAsync(handle, toVerify, cancellationToken);
            var byUrl = checks
                .GroupBy(c => NormalizeUrl(c.Url))
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            foreach (var url in toVerify)
            {
                if (!byUrl.TryGetValue(NormalizeUrl(url), out var check))
                {
                    continue;
                }

                var decision = check.Status switch
                {
                    PlatformLinkStatuses.Confirmed => (State: ProfileMatchState.Confirmed, Confidence: options.ConfirmedConfidence, Signal: ConfirmedSignal),
                    PlatformLinkStatuses.Conflict => (State: ProfileMatchState.Held, Confidence: options.HeldConflictConfidence, Signal: ConflictSignal),
                    PlatformLinkStatuses.Unknown => (State: ProfileMatchState.Held, Confidence: options.HeldUnknownConfidence, Signal: UnknownSignal),
                    _ => ((ProfileMatchState State, double Confidence, MatchSignal Signal)?)null,
                };

                if (decision is var (state, confidence, signal))
                {
                    await _matchStore.RecordAutomaticAsync(memberProfileId, url, state, confidence, [signal], cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not check the links of profile {MemberProfileId}; they stay unchecked", memberProfileId);
        }
    }

    /// <summary>
    /// For each of <paramref name="urls"/> that has a stored decision: "Confirmed", "Held" or "Rejected". Links
    /// without one are left out. Empty when the decisions cannot be loaded.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetLinkStatusesAsync(
        int memberProfileId, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        try
        {
            var matches = await _matchStore.GetForProfileAsync(memberProfileId, cancellationToken);
            var byUrl = matches.Values
                .GroupBy(m => NormalizeUrl(m.SourceUrl))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.DecidedUtc).First().State, StringComparer.Ordinal);

            var statuses = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var url in urls.Distinct(StringComparer.Ordinal))
            {
                if (byUrl.TryGetValue(NormalizeUrl(url), out var state))
                {
                    statuses[url] = state.ToString();
                }
            }

            return statuses;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not load the link decisions of profile {MemberProfileId}", memberProfileId);
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// The accounts and feeds the platform ties to the member that are not among <paramref name="memberLinkUrls"/> and
    /// have not been rejected. Empty when the platform fails or is not configured.
    /// </summary>
    public async Task<IReadOnlyList<LinkSuggestion>> GetSuggestionsAsync(
        string handle, IReadOnlyList<string> memberLinkUrls, CancellationToken cancellationToken = default)
    {
        PlatformProfile? profile;
        IReadOnlyList<string> rejected;
        try
        {
            profile = await _syncClient.FindProfileAsync(handle, cancellationToken);
            if (profile == null)
            {
                return [];
            }

            rejected = await _matchStore.GetRejectedSourcesAsync(handle, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not load link suggestions for {Handle}", handle);
            return [];
        }

        var known = new HashSet<string>(memberLinkUrls.Concat(rejected).Select(NormalizeUrl), StringComparer.Ordinal);

        var candidates = profile.Identities
            .Select(i => new LinkSuggestion(i.ProfileUrl.Trim(), i.Platform, i.Username, LinkSuggestionKinds.Account))
            .Concat(profile.Feeds.Select(f => new LinkSuggestion(f.Url.Trim(), null, f.Name, LinkSuggestionKinds.Feed)));

        var suggestions = new List<LinkSuggestion>();
        foreach (var suggestion in candidates)
        {
            if (ProfileLinkUrl.TryParse(suggestion.Url, out _) && known.Add(NormalizeUrl(suggestion.Url)))
            {
                suggestions.Add(suggestion);
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Records that the member says <paramref name="sourceUrl"/> is not theirs, so activity from it stops showing on
    /// their profile and a reviewer sees the decision. False when the address is not a valid https link, is the
    /// member's own GitHub profile, or the profile does not exist.
    /// </summary>
    public async Task<bool> RejectSourceAsync(int memberProfileId, string handle, string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (!ProfileLinkUrl.TryParse(sourceUrl, out _))
        {
            return false;
        }

        var url = sourceUrl.Trim();
        if (url.Length > ProfileMatchEntity.MaxSourceUrlLength || IsOwnGitHubProfile(url, handle))
        {
            return false;
        }

        return await _matchStore.RecordMemberRejectionAsync(memberProfileId, url, [MemberRejectionSignal], cancellationToken) != null;
    }

    /// <summary>True when <paramref name="url"/> is https://github.com/{handle}, ignoring case and a trailing slash.</summary>
    internal static bool IsOwnGitHubProfile(string url, string handle)
    {
        if (string.IsNullOrWhiteSpace(handle) || !ProfileLinkUrl.TryParse(url, out var uri))
        {
            return false;
        }

        return string.Equals(StripWww(uri.Host), "github.com", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.Query)
            && string.Equals(uri.AbsolutePath.Trim('/'), handle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when activity at <paramref name="activityUrl"/> comes from the source <paramref name="sourceUrl"/>: the same
    /// host (ignoring case and a leading "www."), and either the source is the whole site, the source is a feed
    /// (so anything on its host counts), or the activity sits at or under the source's path.
    /// </summary>
    public static bool SourceCovers(string sourceUrl, string activityUrl)
    {
        if (!Uri.TryCreate(sourceUrl?.Trim(), UriKind.Absolute, out var source)
            || !Uri.TryCreate(activityUrl?.Trim(), UriKind.Absolute, out var activity)
            || string.IsNullOrEmpty(source.Host)
            || !string.Equals(StripWww(source.Host), StripWww(activity.Host), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sourcePath = source.AbsolutePath.TrimEnd('/');
        if (sourcePath.Length == 0 || LooksLikeFeed(sourcePath))
        {
            return true;
        }

        var activityPath = activity.AbsolutePath.TrimEnd('/');
        return activityPath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)
            || activityPath.StartsWith(sourcePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A link reduced for comparison: trimmed, scheme and host lower case, no fragment and no trailing slash.</summary>
    internal static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed.TrimEnd('/');
        }

        return uri.GetLeftPart(UriPartial.Query).TrimEnd('/');
    }

    private static bool LooksLikeFeed(string path)
    {
        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        return FeedFileNames.Contains(lastSegment, StringComparer.OrdinalIgnoreCase)
            || FeedExtensions.Any(e => lastSegment.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
}

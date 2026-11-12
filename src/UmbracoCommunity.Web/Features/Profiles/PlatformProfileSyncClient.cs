using Microsoft.Extensions.Logging;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// <see cref="IProfileSyncClient"/> backed by the external content platform's API (the same address and key
/// as <see cref="CommunityBlogsApiClient"/>). Reads and link checks are real; the platform has no endpoints yet for
/// profile-claim, feed and activity notifications, so those calls just log what they would have sent.
/// </summary>
public sealed class PlatformProfileSyncClient : IProfileSyncClient
{
    /// <summary>The most links the platform checks in one request.</summary>
    private const int MaxLinksPerCheck = 20;

    private readonly CommunityBlogsApiClient _apiClient;
    private readonly ILogger<PlatformProfileSyncClient> _logger;

    public PlatformProfileSyncClient(CommunityBlogsApiClient apiClient, ILogger<PlatformProfileSyncClient> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<PlatformProfile?> FindProfileAsync(string handle, CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured)
        {
            return null;
        }

        var profile = await _apiClient.LookupProfileAsync(handle, cancellationToken);
        if (profile == null)
        {
            return null;
        }

        return new PlatformProfile(profile.Id, profile.MvpYears ?? [])
        {
            Identities = (profile.Identities ?? [])
                .Where(i => !string.IsNullOrWhiteSpace(i.ProfileUrl))
                .Select(i => new PlatformIdentity(i.Platform, i.Username, i.ProfileUrl!.Trim()))
                .ToList(),
            Feeds = (profile.Feeds ?? [])
                .Where(f => !string.IsNullOrWhiteSpace(f.Url))
                .Select(f => new PlatformFeed(f.Url!.Trim(), f.Name))
                .ToList(),
        };
    }

    public async Task<IReadOnlyList<PlatformLinkCheck>> VerifyLinksAsync(
        string handle, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured)
        {
            return [];
        }

        var links = urls
            .Select(u => u.Trim())
            .Where(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var checks = new List<PlatformLinkCheck>();
        foreach (var chunk in links.Chunk(MaxLinksPerCheck))
        {
            var results = await _apiClient.VerifyLinksAsync(handle, chunk, cancellationToken);
            checks.AddRange(results
                .Where(r => !string.IsNullOrWhiteSpace(r.Url) && !string.IsNullOrWhiteSpace(r.Status))
                .Select(r => new PlatformLinkCheck(r.Url!, r.Status!.Trim().ToLowerInvariant(), r.Platform)));
        }

        return checks;
    }

    public async Task<IReadOnlyList<PlatformActivity>> GetActivitiesAsync(
        string platformProfileId, string group, int limit, CancellationToken cancellationToken = default)
    {
        if (!_apiClient.IsConfigured)
        {
            return [];
        }

        var activities = await _apiClient.GetProfileActivitiesAsync(platformProfileId, group, limit, cancellationToken);
        return activities
            .Select(a => new PlatformActivity(a.Id, a.Type, a.Platform, a.Title, a.Url, a.Summary, a.ImageUrl, a.OccurredAt))
            .ToList();
    }

    public Task NotifyProfileClaimedAsync(string handle, string? platformProfileId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile sync: would notify profile claim for {Handle}",
            handle);
        return Task.CompletedTask;
    }

    public Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile sync: would notify feed added for {Handle} ({Platform}: {Url})",
            handle, platform, url);
        return Task.CompletedTask;
    }

    public Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile sync: would notify feed {State} for {Handle} ({Url})",
            isHidden ? "hidden" : "unhidden", handle, url);
        return Task.CompletedTask;
    }

    public Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile sync: would notify feed removed for {Handle} ({Url}), reason: {Reason}",
            handle, url, reason ?? "(none given)");
        return Task.CompletedTask;
    }

    public Task NotifyActivityVerifiedAsync(string handle, ActivityEntryRecord entry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile sync: would notify activity verified for {Handle} ({Kind}: {Url})",
            handle, entry.Kind, entry.Url);
        return Task.CompletedTask;
    }

    public Task NotifyActivityRemovedAsync(string handle, string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile sync: would notify activity removed for {Handle} ({Url})",
            handle, url);
        return Task.CompletedTask;
    }

    /// <summary>The platform has no sync-status endpoint, so there is no timestamp to report.</summary>
    public Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);
}

using Microsoft.Extensions.Logging;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// <see cref="IProfileSyncClient"/> backed by the external content platform's API (the same address and key
/// as <see cref="CommunityBlogsApiClient"/>). Reads are real; the platform has no endpoints yet for
/// profile-claim and feed notifications, so those calls just log what they would have sent.
/// </summary>
public sealed class PlatformProfileSyncClient : IProfileSyncClient
{
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
        return profile == null ? null : new PlatformProfile(profile.Id, profile.MvpYears ?? []);
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

    /// <summary>The platform has no sync-status endpoint, so there is no timestamp to report.</summary>
    public Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);
}

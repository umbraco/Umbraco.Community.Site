using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// No-op <see cref="IProfileSyncClient"/> — the external content platform doesn't expose
/// profile/feed endpoints yet, so every call just logs what it would have sent. Swap this
/// registration in <see cref="RegisterMemberProfiles"/> for a real client once those endpoints exist.
/// </summary>
public sealed class StubProfileSyncClient : IProfileSyncClient
{
    private readonly ILogger<StubProfileSyncClient> _logger;

    public StubProfileSyncClient(ILogger<StubProfileSyncClient> logger)
    {
        _logger = logger;
    }

    public Task NotifyProfileClaimedAsync(string handle, string? platformProfileId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile-sync stub: would notify profile claim for {Handle} (the platform has no profile/feed endpoints yet)",
            handle);
        return Task.CompletedTask;
    }

    public Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile-sync stub: would notify feed added for {Handle} ({Platform}: {Url})",
            handle, platform, url);
        return Task.CompletedTask;
    }

    public Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile-sync stub: would notify feed {State} for {Handle} ({Url})",
            isHidden ? "hidden" : "unhidden", handle, url);
        return Task.CompletedTask;
    }

    public Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile-sync stub: would notify feed removed for {Handle} ({Url}), reason: {Reason}",
            handle, url, reason ?? "(none given)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// The external platform has no sync-status endpoint yet either, so this makes up a plausible
    /// recent timestamp rather than returning null every time — lets the account page's "last
    /// synced" line be designed and reviewed against realistic data before the real endpoint exists.
    /// </summary>
    public Task<DateTimeOffset?> GetLastSyncedAtAsync(string handle, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Profile-sync stub: would fetch last-synced timestamp for {Handle}",
            handle);
        return Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow.AddHours(-6));
    }
}

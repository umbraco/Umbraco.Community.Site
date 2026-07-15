using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// No-op <see cref="ISphereProfileSyncClient"/> — Sphere doesn't expose profile/feed
/// endpoints yet, so every call just logs what it would have sent. Swap this registration
/// in <see cref="RegisterMemberProfiles"/> for a real client once those endpoints exist.
/// </summary>
public sealed class StubSphereProfileSyncClient : ISphereProfileSyncClient
{
    private readonly ILogger<StubSphereProfileSyncClient> _logger;

    public StubSphereProfileSyncClient(ILogger<StubSphereProfileSyncClient> logger)
    {
        _logger = logger;
    }

    public Task NotifyProfileClaimedAsync(string handle, string? sphereProfileId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sphere profile-sync stub: would notify profile claim for {Handle} (Sphere has no profile/feed endpoints yet)",
            handle);
        return Task.CompletedTask;
    }

    public Task NotifyFeedAddedAsync(string handle, string platform, string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sphere profile-sync stub: would notify feed added for {Handle} ({Platform}: {Url})",
            handle, platform, url);
        return Task.CompletedTask;
    }

    public Task NotifyFeedHiddenAsync(string handle, string url, bool isHidden, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sphere profile-sync stub: would notify feed {State} for {Handle} ({Url})",
            isHidden ? "hidden" : "unhidden", handle, url);
        return Task.CompletedTask;
    }

    public Task NotifyFeedRemovedAsync(string handle, string url, string? reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sphere profile-sync stub: would notify feed removed for {Handle} ({Url}), reason: {Reason}",
            handle, url, reason ?? "(none given)");
        return Task.CompletedTask;
    }
}

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Members;

/// <summary>
/// Puts members who are a current MVP or an Umbraco teammate into the early-access group when they sign in.
/// A current MVP is listed in the latest year of <c>mvp.json</c>; a teammate is marked as one on the external
/// content platform. Both are matched on GitHub handle (the member's <c>UserName</c>). Only ever adds the group:
/// editors can also grant it by hand from the "Groups" tab, so a member who is no longer an MVP or a teammate
/// keeps it until an editor removes it.
/// </summary>
internal sealed class EarlyAccessLoginHandler : INotificationAsyncHandler<MemberLoginSuccessNotification>
{
    private readonly IExternalMemberService _externalMemberService;
    private readonly IMvpDataService _mvpDataService;
    private readonly IProfileSyncClient _syncClient;
    private readonly ILogger<EarlyAccessLoginHandler> _logger;

    public EarlyAccessLoginHandler(
        IExternalMemberService externalMemberService,
        IMvpDataService mvpDataService,
        IProfileSyncClient syncClient,
        ILogger<EarlyAccessLoginHandler> logger)
    {
        _externalMemberService = externalMemberService;
        _mvpDataService = mvpDataService;
        _syncClient = syncClient;
        _logger = logger;
    }

    public async Task HandleAsync(MemberLoginSuccessNotification notification, CancellationToken cancellationToken)
    {
        // A failure here must never block a sign-in.
        try
        {
            if (notification.MemberKey is not { } memberKey)
            {
                return;
            }

            // Content-based members (not yet migrated) are not external members: skipped.
            var member = await _externalMemberService.GetByKeyAsync(memberKey);
            if (member == null || string.IsNullOrWhiteSpace(member.UserName))
            {
                return;
            }

            var roles = await _externalMemberService.GetRolesAsync(member.Key);
            if (roles.Contains(ProfileAccessService.EarlyAccessGroupName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            // The MVP list is local; the platform is only asked about members it would make a difference for.
            var reason = IsCurrentMvp(member.UserName) ? "current MVP"
                : await IsTeammateAsync(member.UserName, cancellationToken) ? "teammate"
                : null;
            if (reason == null)
            {
                return;
            }

            var result = await _externalMemberService.AssignRolesAsync(member.Key, [ProfileAccessService.EarlyAccessGroupName]);
            if (result.Success)
            {
                _logger.LogInformation(
                    "Added {Reason} {UserName} ({MemberKey}) to the {Group} member group",
                    reason, member.UserName, member.Key, ProfileAccessService.EarlyAccessGroupName);
            }
            else
            {
                _logger.LogWarning(
                    "Could not add {Reason} {UserName} ({MemberKey}) to the {Group} member group: {Status}",
                    reason, member.UserName, member.Key, ProfileAccessService.EarlyAccessGroupName, result.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not check early-access membership for member {MemberKey}", notification.MemberKey);
        }
    }

    /// <summary>How long a sign-in waits for the platform's answer before carrying on without it.</summary>
    internal static readonly TimeSpan TeammateLookupTimeout = TimeSpan.FromSeconds(4);

    /// <summary>
    /// True when the platform marks the member as a teammate. A slow or failing platform counts as "no": the member
    /// is asked about again on their next sign-in.
    /// </summary>
    private async Task<bool> IsTeammateAsync(string handle, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TeammateLookupTimeout);
        try
        {
            PlatformProfile? profile = await _syncClient.FindProfileAsync(handle, timeout.Token);
            return profile?.IsTeammate == true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ask the content platform whether {UserName} is a teammate", handle);
            return false;
        }
    }

    private bool IsCurrentMvp(string handle) =>
        _mvpDataService.GetLatest()?.Members.Any(m =>
            string.Equals(m.GitHubHandle, handle, StringComparison.OrdinalIgnoreCase)) == true;
}

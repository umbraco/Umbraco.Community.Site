using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Profiles;

namespace UmbracoCommunity.Web.Features.Members;

/// <summary>
/// Puts members who are a current MVP into the early-access group when they sign in. "Current"
/// means listed in the latest year of <c>mvp.json</c>, matched on GitHub handle (the member's
/// <c>UserName</c>). Only ever adds the group: editors can also grant it by hand from the
/// "Groups" tab, so a member who is no longer an MVP keeps it until an editor removes it.
/// </summary>
internal sealed class MvpEarlyAccessLoginHandler : INotificationAsyncHandler<MemberLoginSuccessNotification>
{
    private readonly IExternalMemberService _externalMemberService;
    private readonly IMvpDataService _mvpDataService;
    private readonly ILogger<MvpEarlyAccessLoginHandler> _logger;

    public MvpEarlyAccessLoginHandler(
        IExternalMemberService externalMemberService,
        IMvpDataService mvpDataService,
        ILogger<MvpEarlyAccessLoginHandler> logger)
    {
        _externalMemberService = externalMemberService;
        _mvpDataService = mvpDataService;
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
            if (member == null || !IsCurrentMvp(member.UserName))
            {
                return;
            }

            var roles = await _externalMemberService.GetRolesAsync(member.Key);
            if (roles.Contains(ProfileAccessService.EarlyAccessGroupName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            var result = await _externalMemberService.AssignRolesAsync(member.Key, [ProfileAccessService.EarlyAccessGroupName]);
            if (result.Success)
            {
                _logger.LogInformation(
                    "Added current MVP {UserName} ({MemberKey}) to the {Group} member group",
                    member.UserName, member.Key, ProfileAccessService.EarlyAccessGroupName);
            }
            else
            {
                _logger.LogWarning(
                    "Could not add current MVP {UserName} ({MemberKey}) to the {Group} member group: {Status}",
                    member.UserName, member.Key, ProfileAccessService.EarlyAccessGroupName, result.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not check MVP early-access membership for member {MemberKey}", notification.MemberKey);
        }
    }

    private bool IsCurrentMvp(string? handle) =>
        !string.IsNullOrWhiteSpace(handle) &&
        _mvpDataService.GetLatest()?.Members.Any(m =>
            string.Equals(m.GitHubHandle, handle, StringComparison.OrdinalIgnoreCase)) == true;
}

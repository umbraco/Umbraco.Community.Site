using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Decides what the current request may see on a community profile page.
/// <list type="bullet">
/// <item>The profile itself: a hidden profile is loaded only for its signed-in owner
/// (see <see cref="GetViewableProfileAsync"/>). Everyone else gets what an unknown handle gets.</item>
/// <item>The enriched sections: shown to everyone when the tenant's Settings node has
/// <c>showEnrichedProfileSections</c> switched on, otherwise only to a signed-in member of the
/// <see cref="EarlyAccessGroupName"/> member group viewing their own profile.</item>
/// </list>
/// Any failure resolves to the most restrictive answer.
/// </summary>
/// <remarks>
/// Scoped, so each answer is worked out once per request and shared by the render controller
/// and <c>SeoDataService</c>.
/// </remarks>
public sealed class ProfileAccessService
{
    /// <summary>The member group (created in the backoffice) whose members see the sections on their own profile.</summary>
    public const string EarlyAccessGroupName = "Early access";

    private readonly IMemberManager _memberManager;
    private readonly IProfileDataProvider _profileDataProvider;
    private readonly ILogger<ProfileAccessService> _logger;
    private readonly Dictionary<string, EnrichedSectionsAccess> _resolvedAccess = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ViewableProfile> _resolvedProfiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentMemberHandle;
    private bool _currentMemberResolved;

    public ProfileAccessService(
        IMemberManager memberManager,
        IProfileDataProvider profileDataProvider,
        ILogger<ProfileAccessService> logger)
    {
        _memberManager = memberManager;
        _profileDataProvider = profileDataProvider;
        _logger = logger;
    }

    /// <summary>The pure decision: open to all, or the viewer is in early access and looking at their own profile.</summary>
    public static bool CanSeeEnrichedSections(bool openToAll, bool isOwnProfile, bool isInEarlyAccess) =>
        openToAll || (isOwnProfile && isInEarlyAccess);

    /// <summary>True when the signed-in member's handle matches <paramref name="handle"/> (ignoring case).</summary>
    public async Task<bool> IsOwnProfileAsync(string handle)
    {
        var currentHandle = await GetCurrentMemberHandleAsync();
        return currentHandle != null && string.Equals(currentHandle, handle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads the profile for <paramref name="handle"/> as the current viewer may see it. A
    /// hidden profile is returned (as a hidden preview) only to its owner.
    /// </summary>
    public async Task<ViewableProfile> GetViewableProfileAsync(string handle, CancellationToken cancellationToken = default)
    {
        if (_resolvedProfiles.TryGetValue(handle, out var cached))
        {
            return cached;
        }

        var viewable = await LoadViewableProfileAsync(handle, cancellationToken);
        _resolvedProfiles[handle] = viewable;
        return viewable;
    }

    /// <summary>Resolves enriched section access for the profile with the given <paramref name="handle"/> on the current request.</summary>
    public async Task<EnrichedSectionsAccess> ResolveAsync(IPublishedContent currentPage, string handle)
    {
        if (_resolvedAccess.TryGetValue(handle, out var cached))
        {
            return cached;
        }

        var access = await ResolveUncachedAsync(currentPage, handle);
        _resolvedAccess[handle] = access;
        return access;
    }

    private async Task<ViewableProfile> LoadViewableProfileAsync(string handle, CancellationToken cancellationToken)
    {
        var profile = await _profileDataProvider.GetProfileAsync(handle, includeHidden: false, cancellationToken);
        if (profile != null)
        {
            return new ViewableProfile(profile, IsHiddenPreview: false);
        }

        if (!await IsOwnProfileAsync(handle))
        {
            return ViewableProfile.None;
        }

        var hiddenProfile = await _profileDataProvider.GetProfileAsync(handle, includeHidden: true, cancellationToken);
        return hiddenProfile != null
            ? new ViewableProfile(hiddenProfile, IsHiddenPreview: true)
            : ViewableProfile.None;
    }

    private async Task<string?> GetCurrentMemberHandleAsync()
    {
        if (_currentMemberResolved)
        {
            return _currentMemberHandle;
        }

        try
        {
            if (_memberManager.IsLoggedIn())
            {
                var currentMember = await _memberManager.GetCurrentMemberAsync();
                _currentMemberHandle = currentMember?.UserName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve the signed-in member; treating the request as anonymous.");
            _currentMemberHandle = null;
        }

        _currentMemberResolved = true;
        return _currentMemberHandle;
    }

    // Deliberately not IMemberManager.IsMemberAuthorizedAsync: in Umbraco 18 that method
    // int.Parse()s the identity Id, which is a GUID for lightweight external members, so it
    // throws for every member who signed up through GitHub. GetRolesAsync reads groups from
    // the right store for both content-based and external-only members.
    internal async Task<bool> IsCurrentMemberInEarlyAccessAsync()
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return false;
        }

        var roles = await _memberManager.GetRolesAsync(member);
        return roles.Contains(EarlyAccessGroupName, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<EnrichedSectionsAccess> ResolveUncachedAsync(IPublishedContent currentPage, string handle)
    {
        try
        {
            var openToAll = currentPage.GetSiteSettings()?.ShowEnrichedProfileSections ?? false;
            if (openToAll)
            {
                return new EnrichedSectionsAccess(Show: true, IsGated: false);
            }

            var isOwnProfile = await IsOwnProfileAsync(handle);
            if (!isOwnProfile)
            {
                return EnrichedSectionsAccess.None;
            }

            var isInEarlyAccess = await IsCurrentMemberInEarlyAccessAsync();

            var show = CanSeeEnrichedSections(openToAll, isOwnProfile, isInEarlyAccess);
            return new EnrichedSectionsAccess(Show: show, IsGated: show);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve profile section access; hiding the enriched sections.");
            return EnrichedSectionsAccess.None;
        }
    }
}

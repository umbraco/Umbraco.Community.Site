using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Decides what the current request may see on a community profile page.
/// <list type="bullet">
/// <item>The profile itself: a hidden profile is loaded only for its signed-in owner and for a
/// signed-in member of the <see cref="DevRelGroupName"/> member group (see <see cref="GetViewableProfileAsync"/>).
/// Everyone else gets what an unknown handle gets.</item>
/// <item>The enriched sections: shown to everyone when the tenant's Settings node has
/// <c>showEnrichedProfileSections</c> switched on, otherwise only to a signed-in member of the
/// <see cref="EarlyAccessGroupName"/> member group viewing their own profile, and to a signed-in member of the
/// <see cref="DevRelGroupName"/> member group viewing any profile.</item>
/// <item>The display choices (see <see cref="ProfileDisplay"/>): offered to a signed-in member of the
/// <see cref="EarlyAccessGroupName"/> member group, for their own profile.</item>
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

    /// <summary>
    /// The member group (created on start-up by <see cref="Members.Groups.CreateDevRelMemberGroupMigration"/>) whose
    /// members can open any member's profile, including a hidden one, with the enriched sections shown and, on
    /// request, the links, items and sections the owner hid.
    /// </summary>
    public const string DevRelGroupName = "DevRel";

    private readonly IMemberManager _memberManager;
    private readonly IProfileDataProvider _profileDataProvider;
    private readonly ILogger<ProfileAccessService> _logger;
    private readonly Dictionary<string, EnrichedSectionsAccess> _resolvedAccess = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ViewableProfile> _resolvedProfiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentMemberHandle;
    private bool _currentMemberResolved;
    private bool? _isInDevRel;

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
    /// True when the signed-in member is in the <see cref="DevRelGroupName"/> member group and
    /// <paramref name="handle"/> is not their own profile. Such a page is not what the public sees, so it is
    /// served with a noindex robots tag and must not be cached.
    /// </summary>
    public async Task<bool> IsDevRelViewAsync(string handle) =>
        !await IsOwnProfileAsync(handle) && await IsCurrentMemberInDevRelAsync();

    /// <summary>
    /// Loads the profile for <paramref name="handle"/> as the current viewer may see it. A
    /// hidden profile is returned as a hidden preview to its owner, and as a DevRel-only view to a
    /// member of the <see cref="DevRelGroupName"/> member group.
    /// </summary>
    /// <param name="handle">The member's GitHub handle.</param>
    /// <param name="includeHiddenLinks">
    /// Keep the links the member hid, marked with <see cref="ProfileSocialLink.IsHiddenByMember"/>. Honoured only for
    /// a DevRel view (see <see cref="IsDevRelViewAsync"/>); everyone else never gets them.
    /// </param>
    /// <param name="cancellationToken">Cancels loading the profile.</param>
    public async Task<ViewableProfile> GetViewableProfileAsync(
        string handle,
        bool includeHiddenLinks = false,
        CancellationToken cancellationToken = default)
    {
        if (!_resolvedProfiles.TryGetValue(handle, out var viewable))
        {
            viewable = await LoadViewableProfileAsync(handle, cancellationToken);
            _resolvedProfiles[handle] = viewable;
        }

        return includeHiddenLinks && await IsDevRelViewAsync(handle)
            ? viewable
            : WithoutHiddenLinks(viewable);
    }

    private static ViewableProfile WithoutHiddenLinks(ViewableProfile viewable)
    {
        if (viewable.Profile is not { } profile || !viewable.HasHiddenSocialLinks)
        {
            return viewable;
        }

        var identity = profile.Identity;
        return viewable with
        {
            Profile = profile with
            {
                Identity = identity with { SocialLinks = [.. identity.SocialLinks.Where(l => !l.IsHiddenByMember)] },
            },
        };
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

    // A DevRel view loads the links the member hid too, so the request needs one load whichever view it asks for;
    // GetViewableProfileAsync takes them out again unless they were asked for.
    private async Task<ViewableProfile> LoadViewableProfileAsync(string handle, CancellationToken cancellationToken)
    {
        var isDevRelView = await IsDevRelViewAsync(handle);
        var profile = await _profileDataProvider.GetProfileAsync(handle, includeHidden: false, includeHiddenLinks: isDevRelView, cancellationToken);
        if (profile != null)
        {
            return WithHiddenLinksFlag(new ViewableProfile(profile, IsHiddenPreview: false));
        }

        if (await IsOwnProfileAsync(handle))
        {
            var ownHiddenProfile = await _profileDataProvider.GetProfileAsync(handle, includeHidden: true, cancellationToken: cancellationToken);
            return ownHiddenProfile != null
                ? new ViewableProfile(ownHiddenProfile, IsHiddenPreview: true)
                : ViewableProfile.None;
        }

        if (!isDevRelView)
        {
            return ViewableProfile.None;
        }

        var hiddenProfile = await _profileDataProvider.GetProfileAsync(handle, includeHidden: true, includeHiddenLinks: true, cancellationToken);
        if (hiddenProfile == null)
        {
            return ViewableProfile.None;
        }

        _logger.LogInformation(
            "DevRel member {ViewerHandle} viewed the hidden profile {Handle}",
            await GetCurrentMemberHandleAsync(),
            hiddenProfile.Slug);
        return WithHiddenLinksFlag(new ViewableProfile(hiddenProfile, IsHiddenPreview: false, IsHiddenFromPublic: true));
    }

    private static ViewableProfile WithHiddenLinksFlag(ViewableProfile viewable) =>
        viewable.Profile!.Identity.SocialLinks.Any(l => l.IsHiddenByMember)
            ? viewable with { HasHiddenSocialLinks = true }
            : viewable;

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

    /// <summary>
    /// True when the signed-in member may choose what the enriched sections of their own profile show,
    /// which is when they are in the <see cref="EarlyAccessGroupName"/> member group.
    /// </summary>
    public async Task<bool> CanChooseProfileDisplayAsync()
    {
        try
        {
            return await IsCurrentMemberInEarlyAccessAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve the signed-in member's groups; not offering the profile display choices.");
            return false;
        }
    }

    /// <summary>True when the signed-in member may add activity to their own profile by hand: the same group as the display choices.</summary>
    public Task<bool> CanAddActivityAsync() => CanChooseProfileDisplayAsync();

    /// <summary>
    /// True when the signed-in member's links are checked against the external content platform, they are offered
    /// links found for them, and they can say a source is not theirs: the same group as the display choices.
    /// </summary>
    public Task<bool> CanMatchLinksAsync() => CanChooseProfileDisplayAsync();

    internal Task<bool> IsCurrentMemberInEarlyAccessAsync() => IsCurrentMemberInGroupAsync(EarlyAccessGroupName);

    /// <summary>
    /// True when the signed-in member is in the <see cref="DevRelGroupName"/> member group. Worked out once per
    /// request; a failure counts as not in the group.
    /// </summary>
    public async Task<bool> IsCurrentMemberInDevRelAsync()
    {
        if (_isInDevRel is { } resolved)
        {
            return resolved;
        }

        try
        {
            _isInDevRel = await GetCurrentMemberHandleAsync() != null
                && await IsCurrentMemberInGroupAsync(DevRelGroupName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve the signed-in member's groups; not treating them as DevRel.");
            _isInDevRel = false;
        }

        return _isInDevRel.Value;
    }

    // Deliberately not IMemberManager.IsMemberAuthorizedAsync: in Umbraco 18 that method
    // int.Parse()s the identity Id, which is a GUID for lightweight external members, so it
    // throws for every member who signed up through GitHub. GetRolesAsync reads groups from
    // the right store for both content-based and external-only members.
    private async Task<bool> IsCurrentMemberInGroupAsync(string groupName)
    {
        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return false;
        }

        var roles = await _memberManager.GetRolesAsync(member);
        return roles.Contains(groupName, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<EnrichedSectionsAccess> ResolveUncachedAsync(IPublishedContent currentPage, string handle)
    {
        bool openToAll;
        try
        {
            openToAll = currentPage.GetSiteSettings()?.ShowEnrichedProfileSections ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve profile section access; hiding the enriched sections.");
            return EnrichedSectionsAccess.None;
        }

        return await ResolveAsync(openToAll, handle);
    }

    /// <summary>Resolves enriched section access once the tenant's "open to all" setting is known.</summary>
    internal async Task<EnrichedSectionsAccess> ResolveAsync(bool openToAll, string handle)
    {
        try
        {
            if (openToAll)
            {
                return new EnrichedSectionsAccess(Show: true, IsGated: false);
            }

            var isOwnProfile = await IsOwnProfileAsync(handle);
            if (isOwnProfile && CanSeeEnrichedSections(openToAll, isOwnProfile, await IsCurrentMemberInEarlyAccessAsync()))
            {
                return new EnrichedSectionsAccess(Show: true, IsGated: true);
            }

            // DevRel sees the sections on every profile, their own included.
            return await IsCurrentMemberInDevRelAsync()
                ? new EnrichedSectionsAccess(Show: true, IsGated: true)
                : EnrichedSectionsAccess.None;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve profile section access; hiding the enriched sections.");
            return EnrichedSectionsAccess.None;
        }
    }
}

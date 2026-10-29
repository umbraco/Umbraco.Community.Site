using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Models.Pages;

/// <summary>
/// View model for <c>CommunityProfilePage</c>. <see cref="Profile"/> is <c>null</c> when the
/// requested handle hasn't completed onboarding yet, or its profile is hidden and the viewer is
/// not its owner. The view renders a "claim this profile"
/// CTA in that case instead of a 404 (any plausible-looking handle reaches this far; the
/// content finder already rejected garbage paths).
/// </summary>
public class CommunityProfilePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    public required string Handle { get; init; }

    public CommunityProfile? Profile { get; init; }

    /// <summary>True when the current logged-in member's own handle matches this profile.</summary>
    public bool IsOwnProfile { get; init; }

    /// <summary>
    /// True when the owner is looking at their own profile while it is still hidden. The page
    /// shows a banner offering to show the profile, and is served with a noindex robots tag.
    /// </summary>
    public bool IsHiddenPreview { get; init; }

    /// <summary>The tenant's account page URL, linked from the hidden-profile banner. Null when there is no account page.</summary>
    public string? AccountPageUrl { get; init; }

    /// <summary>True when the current request may see the profile's enriched sections (see <c>ProfileAccessService</c>).</summary>
    public bool ShowEnrichedSections { get; init; }

    /// <summary>
    /// True when the enriched sections are shown only because the viewer is in the early access
    /// group looking at their own profile. The page is then served with a noindex robots tag.
    /// </summary>
    public bool IsGated { get; init; }
}

using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Models.Pages;

/// <summary>
/// View model for <c>CommunityProfilePage</c>. <see cref="Profile"/> is <c>null</c> when the
/// requested handle hasn't completed onboarding yet — the view renders a "claim this profile"
/// CTA in that case instead of a 404 (any plausible-looking handle reaches this far; the
/// content finder already rejected garbage paths).
/// </summary>
public class CommunityProfilePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
{
    public required string Handle { get; init; }

    public CommunityProfile? Profile { get; init; }

    /// <summary>True when the current logged-in member's own handle matches this profile.</summary>
    public bool IsOwnProfile { get; init; }
}

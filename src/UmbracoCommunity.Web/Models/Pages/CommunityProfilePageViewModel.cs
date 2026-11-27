using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.Profiles;
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

    /// <summary>The profile's "Featured" strip (see <see cref="ProfileFeatured"/>), each card with its picture when it has a real one.</summary>
    public IReadOnlyList<ProfileFeaturedHighlight> Featured { get; init; } = [];

    /// <summary>True when the current logged-in member's own handle matches this profile.</summary>
    public bool IsOwnProfile { get; init; }

    /// <summary>
    /// True when the owner is looking at their own profile while it is still hidden. The page
    /// shows a banner offering to show the profile, and is served with a noindex robots tag.
    /// </summary>
    public bool IsHiddenPreview { get; init; }

    /// <summary>
    /// The tenant's account page URL, linked from the owner's "Edit profile" link and the
    /// hidden-profile banner. Null for other visitors, or when there is no account page.
    /// </summary>
    public string? AccountPageUrl { get; init; }

    /// <summary>True when the current request may see the profile's enriched sections (see <c>ProfileAccessService</c>).</summary>
    public bool ShowEnrichedSections { get; init; }

    /// <summary>
    /// True when the enriched sections are shown only because the viewer is in the early access group looking at their
    /// own profile. The page is then served with a noindex robots tag.
    /// </summary>
    public bool IsGated { get; init; }

    /// <summary>
    /// The current page of the profile's timeline (see <see cref="ProfileTimeline"/>), newest month first. Empty when the
    /// enriched sections are not shown, there is nothing dated, or nothing matches the type filter.
    /// </summary>
    public IReadOnlyList<ProfileTimelineMonth> Timeline { get; init; } = [];

    /// <summary>The timeline's type filter options: the sections that have entries.</summary>
    public IReadOnlyList<ProfileTimelineType> TimelineTypes { get; init; } = [];

    /// <summary>
    /// The type the timeline is filtered by, from the <c>?type=</c> query string (see <see cref="ProfileTimeline.SelectedType"/>).
    /// Null shows everything.
    /// </summary>
    public string? TimelineType { get; init; }

    /// <summary>Which page of the timeline this is (from 1); <see cref="Timeline"/> holds just that page.</summary>
    public int TimelinePage { get; init; } = 1;

    /// <summary>How many pages the timeline has for <see cref="TimelineType"/>.</summary>
    public int TimelinePageCount { get; init; } = 1;

    /// <summary>How many entries match <see cref="TimelineType"/> across all pages.</summary>
    public int TimelineTotal { get; init; }

    /// <summary>The activity graph, unless the member switched it off, when there was activity in the last 12 months.</summary>
    public ProfileActivityGraph? ActivityGraph { get; init; }

    /// <summary>Set while the timeline is still being built; the page then shows a progress bar and refreshes itself instead of the graph and timeline.</summary>
    public ProfileJudgingProgress? JudgingProgress { get; init; }
}

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
    /// True when the enriched sections are shown only because of the viewer's member group (the early access group
    /// on their own profile, or the DevRel group). The page is then served with a noindex robots tag.
    /// </summary>
    public bool IsGated { get; init; }

    /// <summary>
    /// True when a member of the DevRel member group is looking at someone else's profile (see
    /// <c>ProfileAccessService.IsDevRelViewAsync</c>).
    /// </summary>
    public bool IsDevRelView { get; init; }

    /// <summary>True when the owner has hidden the whole profile and it is shown only because the viewer is DevRel.</summary>
    public bool IsHiddenFromPublic { get; init; }

    /// <summary>True when the member hid some of their links. Set for a DevRel view only.</summary>
    public bool HasHiddenSocialLinks { get; init; }

    /// <summary>
    /// True when a DevRel viewer asked for everything: the sections and items the owner hid are shown, and marked.
    /// </summary>
    public bool IsShowingEverything { get; init; }

    /// <summary>
    /// The profile's timeline (see <see cref="ProfileTimeline"/>), newest month first. Empty when the enriched sections
    /// are not shown or there is nothing dated.
    /// </summary>
    public IReadOnlyList<ProfileTimelineMonth> Timeline { get; init; } = [];

    /// <summary>The timeline's type filter options: the sections that have entries.</summary>
    public IReadOnlyList<ProfileTimelineType> TimelineTypes { get; init; } = [];

    /// <summary>
    /// The type the timeline is filtered by, from the <c>?type=</c> query string (see <see cref="ProfileTimeline.SelectedType"/>).
    /// Null shows everything.
    /// </summary>
    public string? TimelineType { get; init; }

    /// <summary>True when the timeline has a month with an entry that matches <see cref="TimelineType"/>.</summary>
    public bool TimelineHasShownEntries => Timeline.Any(m => m.Entries.Any(e => ProfileTimeline.IsShown(e, TimelineType)));

    /// <summary>The activity graph, unless the member switched it off, when there was activity in the last 12 months.</summary>
    public ProfileActivityGraph? ActivityGraph { get; init; }

    /// <summary>Link to the profile as the public sees it. Set for a DevRel view only.</summary>
    public string? DevRelPublicViewUrl { get; init; }

    /// <summary>Link to the profile with everything shown. Set for a DevRel view only.</summary>
    public string? DevRelShowEverythingUrl { get; init; }

    /// <summary>
    /// True when the page shows a DevRel viewer something an anonymous visitor would not see, or could: the profile
    /// is hidden, the activity sections are not open to the public, or the owner hid links, sections or items.
    /// </summary>
    public bool ShowDevRelBanner =>
        IsDevRelView
        && Profile != null
        && (IsShowingEverything
            || IsHiddenFromPublic
            || IsGated
            || HasHiddenSocialLinks
            || ProfileDisplay.HidesAnything(Profile.DisplayChoices));

    /// <summary>
    /// True when the page shows everything and the member hid <paramref name="link"/>, so the view marks it as hidden.
    /// </summary>
    public bool IsMarkedHidden(ProfileSocialLink link) => IsShowingEverything && link.IsHiddenByMember;

    /// <summary>
    /// True when the page shows everything and the owner hid the section <paramref name="group"/>, or the item with
    /// <paramref name="activityId"/>, so the view marks it as hidden.
    /// </summary>
    public bool IsMarkedHidden(string group, string? activityId = null) =>
        IsShowingEverything
        && Profile != null
        && ProfileDisplay.IsHiddenByMember(Profile.DisplayChoices, group, activityId);
}

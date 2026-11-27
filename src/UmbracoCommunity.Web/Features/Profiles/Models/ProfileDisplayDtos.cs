namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// Everything the member has chosen their profile's activity sections show. Replaces what was stored before,
/// so a section or item left out is shown again and a pin left out is removed. Pins are kept in the order given.
/// </summary>
public sealed record UpdateProfileDisplayRequest(
    IReadOnlyList<string>? HiddenSections,
    IReadOnlyList<string>? HiddenItemIds,
    IReadOnlyList<UpdateProfilePinRequest>? Pins);

public sealed record UpdateProfilePinRequest(string? Id, string? Reason);

/// <summary>A pinned item.</summary>
/// <param name="Title">The item's title as kept with the pin, so a pin that is no longer among the member's
/// latest activity can still be shown. Null for a pin saved before the item was kept with it.</param>
public sealed record ProfilePinResponse(string Id, string? Reason, string? Title = null);

/// <summary>The member's stored choices, as saved.</summary>
public sealed record ProfileDisplayChoicesResponse(
    IReadOnlyList<string> HiddenSections,
    IReadOnlyList<string> HiddenItemIds,
    IReadOnlyList<ProfilePinResponse> Pins);

/// <summary>The member's activity sections, with every item in them (hidden ones too), and their stored choices.</summary>
public sealed record ProfileDisplayResponse(
    IReadOnlyList<ProfileDisplaySection> Sections,
    ProfileDisplayChoicesResponse Choices);

public sealed record ProfileDisplaySection(string Key, string Label, IReadOnlyList<ProfileDisplayItem> Items);

/// <summary>
/// An item in one of the member's activity sections, with what the account page needs to draw it as the public
/// timeline does (see <see cref="ProfileTimeline"/>). The timeline fields are filled from the same
/// <see cref="ProfileTimelineEntry"/> the profile page renders, so the two can't drift.
/// </summary>
public sealed record ProfileDisplayItem(string Id, string Title, DateTimeOffset? Date)
{
    /// <summary>The activity section it comes from (see <see cref="PlatformActivityGroups"/>).</summary>
    public string? Group { get; init; }

    /// <summary>The icon to draw it with (see <c>_KindIcon.cshtml</c> and <c>profile-kind-icons.ts</c>).</summary>
    public string? IconKey { get; init; }

    /// <summary>Where it links to. Null for an item without a page.</summary>
    public string? Url { get; init; }

    /// <summary>The short line of context the timeline shows after the date, e.g. the blog it was posted on.</summary>
    public string? Meta { get; init; }

    public string? Description { get; init; }

    /// <summary>True for a routine package release, which the timeline's highlights leave out (see <see cref="ProfileTimeline.IsShown"/>).</summary>
    public bool IsRoutineRelease { get; init; }

    /// <summary>Its preview card when one is ready. Null when there is no picture (yet).</summary>
    public ProfileTimelineCard? Card { get; init; }

    /// <summary>The month the timeline lists it under, as <c>yyyy-MM</c>. Null when it has no date.</summary>
    public string? MonthKey { get; init; }

    /// <summary>The month heading, e.g. "Mar 2026" (see <see cref="ProfileTimeline.MonthLabel"/>). Null when it has no date.</summary>
    public string? MonthLabel { get; init; }

    /// <summary>Its place on the whole timeline across every section, from 0 (the newest).</summary>
    public int Order { get; init; }
}

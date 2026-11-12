namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// Everything the member has chosen their profile's activity sections show. Replaces what was stored before,
/// so a section or item left out is shown again and a pin left out is removed. Pins are kept in the order given.
/// A switch left out is off, and an item left out of <see cref="NoCardItemIds"/> gets its preview card again.
/// </summary>
/// <param name="HideActivityGraph">Leave the activity graph off the profile.</param>
/// <param name="HideTimelineCards">Show every timeline entry as a plain line, without preview cards.</param>
/// <param name="NoCardItemIds">The ids of single timeline entries shown without a preview card.</param>
public sealed record UpdateProfileDisplayRequest(
    IReadOnlyList<string>? HiddenSections,
    IReadOnlyList<string>? HiddenItemIds,
    IReadOnlyList<UpdateProfilePinRequest>? Pins,
    bool? HideActivityGraph = null,
    bool? HideTimelineCards = null,
    IReadOnlyList<string>? NoCardItemIds = null);

public sealed record UpdateProfilePinRequest(string? Id, string? Reason);

/// <summary>A pinned item.</summary>
/// <param name="Title">The item's title as kept with the pin, so a pin that is no longer among the member's
/// latest activity can still be shown. Null for a pin saved before the item was kept with it.</param>
public sealed record ProfilePinResponse(string Id, string? Reason, string? Title = null);

/// <summary>The member's stored choices, as saved.</summary>
public sealed record ProfileDisplayChoicesResponse(
    IReadOnlyList<string> HiddenSections,
    IReadOnlyList<string> HiddenItemIds,
    IReadOnlyList<ProfilePinResponse> Pins,
    bool HideActivityGraph = false,
    bool HideTimelineCards = false,
    IReadOnlyList<string>? NoCardItemIds = null);

/// <summary>The member's activity sections, with every item in them (hidden ones too), and their stored choices.</summary>
public sealed record ProfileDisplayResponse(
    IReadOnlyList<ProfileDisplaySection> Sections,
    ProfileDisplayChoicesResponse Choices);

public sealed record ProfileDisplaySection(string Key, string Label, IReadOnlyList<ProfileDisplayItem> Items);

public sealed record ProfileDisplayItem(string Id, string Title, DateTimeOffset? Date);

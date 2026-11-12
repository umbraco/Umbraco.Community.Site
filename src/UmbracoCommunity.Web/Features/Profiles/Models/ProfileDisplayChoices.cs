namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// What a member has chosen their profile's activity sections show (see <see cref="ProfileDisplay.Apply"/>).
/// </summary>
/// <param name="HiddenSections">The activity sections (see <see cref="PlatformActivityGroups"/>) switched off.</param>
/// <param name="HiddenActivityIds">The ids of single activities hidden from the profile.</param>
/// <param name="Pins">The activities pinned to the Featured block, in order.</param>
public sealed record ProfileDisplayChoices(
    IReadOnlyList<string> HiddenSections,
    IReadOnlyList<string> HiddenActivityIds,
    IReadOnlyList<ProfilePin> Pins)
{
    /// <summary>Every section shown, nothing hidden, nothing pinned, the activity graph and preview cards on.</summary>
    public static readonly ProfileDisplayChoices None = new([], [], []);

    /// <summary>True when the member has switched the activity graph off. The graph is on by default.</summary>
    public bool HideActivityGraph { get; init; }

    /// <summary>True when the member has switched preview cards off for the whole timeline. Cards are on by default.</summary>
    public bool HideTimelineCards { get; init; }

    /// <summary>The ids of single activities the member wants shown as a plain line in the timeline, without a preview card.</summary>
    public IReadOnlyList<string> NoCardActivityIds { get; init; } = [];
}

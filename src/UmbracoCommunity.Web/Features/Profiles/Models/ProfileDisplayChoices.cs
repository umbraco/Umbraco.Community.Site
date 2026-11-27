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
    /// <summary>Every section shown, nothing hidden, nothing pinned.</summary>
    public static readonly ProfileDisplayChoices None = new([], [], []);
}

namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>A pinned activity as the profile's Featured block shows it, with the member's note on why.</summary>
/// <param name="Group">The activity section it comes from (see <see cref="PlatformActivityGroups"/>).</param>
public sealed record ProfileFeaturedItem(
    string Group,
    string ActivityId,
    string Title,
    string Url,
    string? Description,
    DateTimeOffset? Date,
    string? Source,
    string? Reason);

namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>An activity the member has pinned to their profile's Featured block, with their note on why.</summary>
/// <param name="Snapshot">The item as it was when last saved, shown once it is no longer among the member's
/// latest activity. Null for a pin saved before the item was kept with it.</param>
public sealed record ProfilePin(string ActivityId, string? Reason, ProfileFeaturedItem? Snapshot = null);

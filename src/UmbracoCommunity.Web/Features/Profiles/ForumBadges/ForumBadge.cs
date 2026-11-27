namespace UmbracoCommunity.Web.Features.Profiles.ForumBadges;

/// <summary>
/// A badge a member earned on the Umbraco forum, as the forum's public badge list describes it.
/// </summary>
/// <param name="Id">The forum's badge id, which is what a member's choice of badges to show refers to.</param>
/// <param name="Description">Plain text (the forum's HTML is removed). Null when the forum has none.</param>
/// <param name="ImageUrl">
/// The badge's own picture on the forum (absolute https), or null when the badge only has an icon name. Never put in
/// front of a visitor as it is: pages show it through the image proxy.
/// </param>
/// <param name="BadgeType">"Gold", "Silver" or "Bronze" as the forum names it, or null when not known.</param>
/// <param name="GrantCount">How many forum members have the badge, or null when not given.</param>
/// <param name="GrantedAt">When the member first got it, or null when not given.</param>
/// <param name="Slug">
/// The badge's URL slug (lowercase letters, digits and hyphens only; see <see cref="ForumProfileUrl.BadgeSlug"/>), or
/// null when none could be made. Only used to build the link to the badge's page (<see cref="Url"/>).
/// </param>
public sealed record ForumBadge(
    int Id,
    string Name,
    string? Description,
    string? ImageUrl,
    string? BadgeType,
    int? GrantCount,
    DateTimeOffset? GrantedAt,
    string? Slug = null)
{
    /// <summary>The badge's page on the forum, built from its id and slug (never from an address the forum gave).</summary>
    public string? Url => ForumProfileUrl.BadgeUrl(Id, Slug);
}

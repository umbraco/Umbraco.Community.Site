using Microsoft.AspNetCore.Html;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

/// <summary>
/// The blue profile hero (<c>Views/Partials/Profiles/_ProfileHero.cshtml</c>): photo, name, MVP marks, headline, bio,
/// location and link pills. The public profile page renders it, and the account page renders it as a preview of that page.
/// </summary>
public sealed class ProfileHeroViewModel
{
    public required ProfileIdentity Identity { get; init; }

    /// <summary>
    /// Renders the headline, bio and links even when empty (hidden), with <c>data-profile-hero</c> hooks, so a script can
    /// fill them in when the member edits their profile on the same page (the account page's <c>dc-account-profile-hero</c>).
    /// </summary>
    public bool Live { get; init; }

    /// <summary>
    /// The badges the member chose to show, as a row under the link pills. The row is left out when there are none,
    /// unless <see cref="Live"/> is set (then it is rendered hidden, so it can be filled in).
    /// </summary>
    public IReadOnlyList<ProfileBadge> Badges { get; init; } = [];

    /// <summary>What kind of badge it is in words (e.g. "Gold forum badge"), so its kind never relies on colour alone.</summary>
    public static string BadgeKind(ProfileBadge badge) =>
        string.IsNullOrEmpty(badge.BadgeType) ? "Forum badge" : $"{badge.BadgeType} forum badge";

    /// <summary>The tooltip of a badge: its kind and its description.</summary>
    public static string BadgeTitle(ProfileBadge badge) =>
        string.IsNullOrEmpty(badge.Description) ? BadgeKind(badge) : $"{BadgeKind(badge)}: {badge.Description}";

    /// <summary>The CSS modifier for a badge's kind ("gold", "silver", "bronze"), or null for any other.</summary>
    public static string? BadgeTypeModifier(ProfileBadge badge) =>
        badge.BadgeType?.ToLowerInvariant() switch
        {
            "gold" => "gold",
            "silver" => "silver",
            "bronze" => "bronze",
            _ => null,
        };

    /// <summary>Content after the links, inside the hero's identity column (e.g. the owner's "Edit profile" button).</summary>
    public Func<object?, IHtmlContent>? Footer { get; init; }
}

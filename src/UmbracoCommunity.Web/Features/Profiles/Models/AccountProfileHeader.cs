namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>What the account page's preview of the member's profile hero shows: their identity and the badges they chose to show.</summary>
public sealed record AccountProfileHeader(ProfileIdentity Identity, IReadOnlyList<ProfileBadge> Badges);

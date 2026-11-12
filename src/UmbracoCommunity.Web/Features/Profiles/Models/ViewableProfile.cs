namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// The profile the current request may see for a handle.
/// </summary>
/// <param name="Profile">The profile, or <c>null</c> when the handle has nothing to show this viewer.</param>
/// <param name="IsHiddenPreview">True when the owner is looking at their own profile while it is still hidden.</param>
/// <param name="IsHiddenFromPublic">
/// True when the owner has hidden the profile and it is shown only because the viewer is in the DevRel member group.
/// </param>
public sealed record ViewableProfile(CommunityProfile? Profile, bool IsHiddenPreview, bool IsHiddenFromPublic = false)
{
    public static readonly ViewableProfile None = new(null, false);

    /// <summary>
    /// True when the member hid some of their links. Only worked out for a DevRel view of someone else's profile;
    /// the links themselves are included only when that view asks for everything.
    /// </summary>
    public bool HasHiddenSocialLinks { get; init; }
}

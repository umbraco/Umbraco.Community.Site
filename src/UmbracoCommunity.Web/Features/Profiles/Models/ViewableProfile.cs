namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// The profile the current request may see for a handle.
/// </summary>
/// <param name="Profile">The profile, or <c>null</c> when the handle has nothing to show this viewer.</param>
/// <param name="IsHiddenPreview">True when the owner is looking at their own profile while it is still hidden.</param>
public sealed record ViewableProfile(CommunityProfile? Profile, bool IsHiddenPreview)
{
    public static readonly ViewableProfile None = new(null, false);
}

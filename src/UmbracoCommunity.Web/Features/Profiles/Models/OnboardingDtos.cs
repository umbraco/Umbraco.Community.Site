namespace UmbracoCommunity.Web.Features.Profiles.Models;

public sealed record OnboardingStateResponse(
    string Handle,
    string DisplayName,
    string? Bio,
    string AvatarUrl,
    bool HasCustomAvatar,
    string OnboardingStatus,
    string? GitHubBio);

public sealed record UpdateBioRequest(string? Bio);

public sealed record UpdateHeadlineRequest(string? Headline);

public sealed record UpdateDisplayNameRequest(string? DisplayName);

/// <summary>
/// The member's display name, headline and bio as shown on their profile. Each shown value is
/// the member's own when its <c>HasCustom…</c> flag is true, otherwise the GitHub default next to it.
/// </summary>
public sealed record ProfileDetailsResponse(
    string DisplayName,
    bool HasCustomDisplayName,
    string DefaultDisplayName,
    string? Headline,
    bool HasCustomHeadline,
    string? GitHubHeadline,
    string? Bio,
    bool HasCustomBio,
    string? GitHubBio);

public sealed record UpdateProfileVisibilityRequest(bool Visible);

public sealed record UpdateAvatarResponse(string AvatarUrl, bool HasCustomAvatar);

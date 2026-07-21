namespace UmbracoCommunity.Web.Features.Profiles.Models;

public sealed record OnboardingStateResponse(
    string Handle,
    string DisplayName,
    string? Bio,
    string AvatarUrl,
    bool HasCustomAvatar,
    string OnboardingStatus);

public sealed record UpdateBioRequest(string? Bio);

public sealed record UpdateAvatarResponse(string AvatarUrl, bool HasCustomAvatar);

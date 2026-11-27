namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>Sets the member's bio. Null, empty or whitespace clears it, so no bio is shown.</summary>
public sealed record UpdateBioRequest(string? Bio);

/// <summary>Sets the member's headline. Null, empty or whitespace clears it, so no headline is shown.</summary>
public sealed record UpdateHeadlineRequest(string? Headline);

/// <summary>Sets the member's display name. A name is required: null, empty or whitespace is refused.</summary>
public sealed record UpdateDisplayNameRequest(string? DisplayName);

/// <summary>
/// The member's display name, headline and bio as their profile shows them: always the member's own text, never a
/// GitHub fallback. A null headline or bio is not shown.
/// </summary>
public sealed record ProfileDetailsResponse(string DisplayName, string? Headline, string? Bio);

public sealed record UpdateProfileVisibilityRequest(bool Visible);

public sealed record UpdateAvatarResponse(string AvatarUrl, bool HasCustomAvatar);

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Length limits and normalisation for the member-written profile text (display name, headline and bio).
/// The limits are mirrored client side in <c>profile-onboarding.service.ts</c>.
/// </summary>
public static class ProfileText
{
    // Short by design — the bio renders in the profile page's hero band next to the headline
    // (see community-profile.css .community-profile__bio, max-width: 60ch), not as a full
    // "about me" section, so it needs to stay skimmable rather than wrapping for many lines.
    public const int MaxBioLength = 280;

    // A single line under the display name in the profile page's hero band.
    public const int MaxHeadlineLength = 100;

    // The profile page's hero heading, so it needs to fit on a line or two at display size.
    public const int MaxDisplayNameLength = 60;

    /// <summary>Trims the value; empty or whitespace-only text becomes null.</summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Normalises the value and cuts it to at most <paramref name="maxLength"/> characters,
    /// without splitting a surrogate pair.
    /// </summary>
    public static string? NormalizeAndTruncate(string? value, int maxLength)
    {
        var normalized = Normalize(value);
        if (normalized == null || normalized.Length <= maxLength)
        {
            return normalized;
        }

        var length = char.IsHighSurrogate(normalized[maxLength - 1]) ? maxLength - 1 : maxLength;
        return Normalize(normalized[..length]);
    }
}

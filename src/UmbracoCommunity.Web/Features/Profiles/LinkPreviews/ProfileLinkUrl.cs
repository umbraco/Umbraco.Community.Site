namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

public static class ProfileLinkUrl
{
    public const int MaxLength = 2048;

    /// <summary>Accepts only absolute https URLs without credentials.</summary>
    public static bool TryParse(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxLength
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || string.IsNullOrEmpty(parsed.Host))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}

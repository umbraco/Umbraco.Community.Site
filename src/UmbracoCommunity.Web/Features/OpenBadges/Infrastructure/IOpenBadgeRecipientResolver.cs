namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>Finds the URL a badge is issued to: the member's public profile page on the site the request is for.</summary>
public interface IOpenBadgeRecipientResolver
{
    /// <summary>
    /// The absolute profile URL of <paramref name="handle"/> under <paramref name="baseUri"/>, or null when the site has
    /// no community profile page (then no badge can be issued on it).
    /// </summary>
    string? GetProfileUrl(Uri baseUri, string handle);
}

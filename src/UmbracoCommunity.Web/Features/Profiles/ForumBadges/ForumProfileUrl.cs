using System.Text.RegularExpressions;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles.ForumBadges;

/// <summary>
/// Recognises a member's link to their own Umbraco forum profile (<c>https://forum.umbraco.com/u/{username}</c>, with
/// or without a trailing page, query or fragment) and reads the forum username from it. The forum username is not
/// necessarily the member's GitHub handle, so it only ever comes from the member's own link, never from a guess. The
/// username is the only thing taken from the link: requests always go to <see cref="Host"/> over https, and only a
/// username made of letters, digits, <c>.</c>, <c>_</c> and <c>-</c> (at most <see cref="MaxUsernameLength"/>
/// characters, not only dots) is accepted, so nothing a member types can change which server or path is asked.
/// </summary>
public static partial class ForumProfileUrl
{
    /// <summary>The Umbraco community forum (Discourse).</summary>
    public const string Host = "forum.umbraco.com";

    /// <summary>Discourse allows up to 60 characters; anything longer is not a forum username.</summary>
    public const int MaxUsernameLength = 60;

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    /// <summary>True for a username we are willing to put in a request path.</summary>
    public static bool IsValidUsername(string? username) =>
        !string.IsNullOrEmpty(username)
        && username.Length <= MaxUsernameLength
        && username.Trim('.') != string.Empty
        && UsernamePattern().IsMatch(username);

    /// <summary>Reads the forum username from an https link to a forum profile page. False for any other link.</summary>
    public static bool TryGetUsername(string? url, out string username)
    {
        username = string.Empty;
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort)
        {
            return false;
        }

        // AbsolutePath leaves the query and fragment out; anything after the username (e.g. /summary) is ignored.
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !string.Equals(segments[0], "u", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = Uri.UnescapeDataString(segments[1]);
        if (!IsValidUsername(candidate))
        {
            return false;
        }

        username = candidate;
        return true;
    }

    /// <summary>
    /// The forum username from the member's links: the first link to a forum profile (in the member's order) that they
    /// show, otherwise the first one they hid (it is still their account, they just don't show the link). Removed links
    /// don't count. Null when there is none.
    /// </summary>
    public static string? FindUsername(IEnumerable<MemberFeedEntity> feeds) =>
        feeds
            .Where(f => !f.IsRemoved)
            .OrderBy(f => f.IsHidden)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.AddedUtc)
            .ThenBy(f => f.Id)
            .Select(f => TryGetUsername(f.Url, out var username) ? username : null)
            .FirstOrDefault(u => u != null);

    /// <summary>The longest badge slug used in a link.</summary>
    public const int MaxSlugLength = 100;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NotSlugCharacters();

    /// <summary>
    /// A badge's slug for its link: the forum's own slug when it is plain (lowercase letters, digits and single hyphens,
    /// at most <see cref="MaxSlugLength"/>), otherwise one made from the badge's name, otherwise null.
    /// </summary>
    public static string? BadgeSlug(string? forumSlug, string? name)
    {
        if (forumSlug != null && forumSlug.Length <= MaxSlugLength && SlugPattern().IsMatch(forumSlug))
        {
            return forumSlug;
        }

        var made = NotSlugCharacters().Replace((name ?? string.Empty).ToLowerInvariant(), "-").Trim('-');
        if (made.Length > MaxSlugLength)
        {
            made = made[..MaxSlugLength].TrimEnd('-');
        }

        return made.Length > 0 && SlugPattern().IsMatch(made) ? made : null;
    }

    /// <summary>
    /// A badge's page on the forum: <c>https://forum.umbraco.com/badges/{id}/{slug}</c>, or without the slug when it isn't
    /// plain (the forum finds a badge by its id alone). Null for an id that isn't a positive number.
    /// </summary>
    public static string? BadgeUrl(int id, string? slug)
    {
        if (id <= 0)
        {
            return null;
        }

        var url = $"https://{Host}/badges/{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return slug != null && slug.Length <= MaxSlugLength && SlugPattern().IsMatch(slug) ? $"{url}/{slug}" : url;
    }

    /// <summary>The public profile page of a forum username.</summary>
    public static string ProfileUrl(string username) => $"https://{Host}/u/{Uri.EscapeDataString(username)}";
}

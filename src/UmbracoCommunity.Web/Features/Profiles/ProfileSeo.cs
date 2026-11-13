using Schema.NET;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// What a profile tells search engines and share previews: whether the page may be indexed, its description,
/// and its structured data. Pure, so it can be tested without a request.
/// </summary>
public static class ProfileSeo
{
    private const int MaxDescriptionLength = 200;

    /// <summary>Width and height, in pixels, of the square share image.</summary>
    public const int ShareImageSize = 600;

    /// <summary>
    /// True when the page is what any visitor sees: the profile is shown, and is not a preview for its owner or a view
    /// for DevRel, and the sections are open to everyone rather than shown only because of the viewer's member group.
    /// </summary>
    public static bool IsPubliclyVisible(ViewableProfile viewable, EnrichedSectionsAccess access, bool isDevRelView) =>
        viewable.Profile is not null
        && !viewable.IsHiddenPreview
        && !viewable.IsHiddenFromPublic
        && !isDevRelView
        && access is { Show: true, IsGated: false };

    /// <summary>
    /// The headline and the bio, cut to a length that fits a search result. Empty when the member wrote neither.
    /// </summary>
    public static string Description(ProfileIdentity identity)
    {
        var text = string.Join(" · ", new[] { identity.Headline, identity.Bio }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim()));
        return text.Length > MaxDescriptionLength ? text[..(MaxDescriptionLength - 3)].TrimEnd() + "..." : text;
    }

    /// <summary>
    /// A ProfilePage whose main entity is the member as a Person. Carries only what the page shows to everyone: the
    /// name, description, photo and the links the member chose to show.
    /// </summary>
    public static ProfilePage Schema(CommunityProfile profile, Uri pageUrl, Uri? imageUrl)
    {
        var identity = profile.Identity;
        var person = new Person
        {
            Name = identity.DisplayName,
            Url = pageUrl,
        };

        var description = Description(identity);
        if (description.Length > 0)
        {
            person.Description = description;
        }

        if (imageUrl is not null)
        {
            person.Image = imageUrl;
        }

        var sameAs = identity.SocialLinks
            .Where(l => !l.IsHiddenByMember)
            .Select(l => Uri.TryCreate(l.Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null)
            .OfType<Uri>()
            .ToList();
        if (sameAs.Count > 0)
        {
            person.SameAs = new OneOrMany<Uri>(sameAs);
        }

        return new ProfilePage
        {
            Name = identity.DisplayName,
            Url = pageUrl,
            MainEntity = person,
        };
    }

    /// <summary>
    /// The picture to share, as a small square: an uploaded photo is resized by <paramref name="resizeMedia"/> (which
    /// returns its signed URL), a GitHub avatar is asked for at that size. Anything else is returned as it is.
    /// </summary>
    public static string? ShareImageUrl(ProfileIdentity identity, Func<string, int, string> resizeMedia)
    {
        var url = identity.AvatarUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (identity.HasCustomAvatar && url.StartsWith('/') && !url.Split('?')[0].EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return resizeMedia(url, ShareImageSize);
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            ? $"{url.Split('?')[0]}?size={ShareImageSize}"
            : url;
    }
}

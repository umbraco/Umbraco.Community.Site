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

    /// <summary>The headline, otherwise the bio, cut to a length that fits a search result. Empty when the member wrote neither.</summary>
    public static string Description(ProfileIdentity identity)
    {
        var text = FirstNonBlank(identity.Headline, identity.Bio);
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

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}

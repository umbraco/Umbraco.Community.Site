using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Builds the profile's "Featured" strip: the items the member pinned (<see cref="CommunityProfile.FeaturedItems"/>) when
/// there are any, otherwise the most recent package, talk, blog post and video (each list is newest first).
/// A card only shows a real picture of its own: one the member attached, a video's thumbnail or the linked page's share
/// image, always our stored copy (see <see cref="ProfileTimelinePreviewService"/>). Without one it has no picture.
/// </summary>
public static class ProfileFeatured
{
    /// <summary>
    /// The profile's highlights, without pictures yet. Each one that links to a page carries an
    /// <see cref="ProfileFeaturedHighlight.ImageLookup"/> to resolve its picture with (see <see cref="WithImages"/>).
    /// </summary>
    public static IReadOnlyList<ProfileFeaturedHighlight> Highlights(CommunityProfile profile)
    {
        var highlights = new List<ProfileFeaturedHighlight>();

        if (profile.FeaturedItems.Count > 0)
        {
            // The member pinned these themselves, in this order, with their own note on why.
            foreach (var item in profile.FeaturedItems)
            {
                var kind = item.Group switch
                {
                    PlatformActivityGroups.Blog => "Blog post",
                    PlatformActivityGroups.Videos => "Video",
                    PlatformActivityGroups.Packages => "Package",
                    PlatformActivityGroups.Forum => "Forum post",
                    PlatformActivityGroups.Meetups => "Meet-up",
                    PlatformActivityGroups.Other => item.Source ?? "Activity",
                    _ => "Social post",
                };

                // A video still loaded has its thumbnail; a pinned copy (or anything else) relies on its page's own picture.
                var thumbnail = item.Group == PlatformActivityGroups.Videos
                    ? profile.Videos.FirstOrDefault(v => v.ActivityId == item.ActivityId)?.ThumbnailUrl
                    : null;

                highlights.Add(new ProfileFeaturedHighlight(kind, ProfileTimeline.IconKey(item.Group), item.Title, item.Url)
                {
                    Meta = Join(item.Date is { } date ? Date(date) : null, item.Source),
                    Description = item.Description,
                    Reason = item.Reason,
                    ImageLookup = Lookup(profile, item.Group, item.ActivityId, item.Title, item.Url, item.Date, thumbnail),
                });
            }

            return highlights;
        }

        if (profile.Packages.Count > 0)
        {
            var p = profile.Packages[0];
            highlights.Add(new ProfileFeaturedHighlight("Package", "package", p.Name, p.Url)
            {
                Description = p.Description,
                Chips = p.Versions,
                ImageLookup = Lookup(profile, PlatformActivityGroups.Packages, p.ActivityId, p.Name, p.Url, p.Date, null),
            });
        }

        if (profile.Talks.Count > 0)
        {
            // Talks are not activity: no activity id, so no picture of the member's own.
            var t = profile.Talks[0];
            highlights.Add(new ProfileFeaturedHighlight("Talk", "talk", t.Title, t.Url)
            {
                Meta = Join(t.EventName, t.Date is { } date ? Date(date) : null),
                Description = t.Description,
                ImageLookup = Lookup(profile, "talk", null, t.Title, t.Url, t.Date, null),
            });
        }

        if (profile.BlogPosts.Count > 0)
        {
            var b = profile.BlogPosts[0];
            highlights.Add(new ProfileFeaturedHighlight("Blog post", "blog", b.Title, b.Url)
            {
                Meta = Join(Date(b.PublishedAt), b.Source),
                Description = b.Excerpt,
                ImageLookup = Lookup(profile, PlatformActivityGroups.Blog, b.ActivityId, b.Title, b.Url, b.PublishedAt, null),
            });
        }

        if (profile.Videos.Count > 0)
        {
            var v = profile.Videos[0];
            highlights.Add(new ProfileFeaturedHighlight("Video", "video", v.Title, v.Url)
            {
                Meta = v.PublishedAt is { } date ? Date(date) : null,
                Description = v.Description,
                ImageLookup = Lookup(profile, PlatformActivityGroups.Videos, v.ActivityId, v.Title, v.Url, v.PublishedAt, v.ThumbnailUrl),
            });
        }

        return highlights;
    }

    /// <summary>
    /// The <paramref name="highlights"/> with the picture of each that has one, taken from the card
    /// <see cref="ProfileTimelinePreviewService.AddCardsAsync"/> gave its <see cref="ProfileFeaturedHighlight.ImageLookup"/>.
    /// <paramref name="carded"/> holds those cards, in the order of the highlights that have a lookup.
    /// </summary>
    public static IReadOnlyList<ProfileFeaturedHighlight> WithImages(
        IReadOnlyList<ProfileFeaturedHighlight> highlights,
        IReadOnlyList<ProfileTimelineEntry> carded)
    {
        var index = 0;
        return highlights
            .Select(h => h.ImageLookup == null
                ? h
                : (index < carded.Count ? carded[index++].Card?.ImageUrl : null) is { } image
                    ? h with { ImageUrl = image }
                    : h)
            .ToList();
    }

    /// <summary>The entries to resolve the highlights' pictures with, in order (see <see cref="WithImages"/>).</summary>
    public static IReadOnlyList<ProfileTimelineEntry> ImageLookups(IEnumerable<ProfileFeaturedHighlight> highlights) =>
        highlights.Select(h => h.ImageLookup).OfType<ProfileTimelineEntry>().ToList();

    /// <summary>
    /// A timeline entry standing in for the highlight, so its picture is found exactly as a timeline card's is: the
    /// member's own picture first, then a known picture (<paramref name="imageUrl"/>), then the page's. Null without a page.
    /// </summary>
    private static ProfileTimelineEntry? Lookup(
        CommunityProfile profile, string group, string? activityId, string title, string? url, DateTimeOffset? date, string? imageUrl)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        return new ProfileTimelineEntry(group, activityId, title, url, null, date ?? DateTimeOffset.MinValue, null, imageUrl)
        {
            OwnImageUrl = activityId != null ? profile.ActivityImages.GetValueOrDefault(activityId) : null,
        };
    }

    private static string Date(DateTimeOffset value) => value.ToString("d MMM yyyy");

    private static string? Join(params string?[] parts)
    {
        var joined = string.Join(" · ", parts.Where(s => !string.IsNullOrEmpty(s)));
        return joined.Length > 0 ? joined : null;
    }
}

/// <summary>A card in the profile's "Featured" strip.</summary>
/// <param name="Kind">What it is, as shown above its title (e.g. "Blog post").</param>
/// <param name="IconKey">Its icon (see <c>_KindIcon.cshtml</c>).</param>
public sealed record ProfileFeaturedHighlight(string Kind, string IconKey, string Title, string? Url)
{
    public string? Meta { get; init; }

    public string? Description { get; init; }

    /// <summary>Version chips (a package's versions).</summary>
    public IReadOnlyList<string>? Chips { get; init; }

    /// <summary>The member's own note on why they pinned it.</summary>
    public string? Reason { get; init; }

    /// <summary>Our stored copy of its picture (see <see cref="ProfileTimelineImageStore"/>), or null when it has none.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>The entry its picture is resolved with (see <see cref="ProfileFeatured.WithImages"/>). Null when it links nowhere.</summary>
    public ProfileTimelineEntry? ImageLookup { get; init; }

    /// <summary>
    /// True when the card shows its picture (linked to the item). Otherwise it gets the default blog cover art
    /// (<c>/img/community-blogs/placeholder-cover.svg</c>) in the same frame, so every card in the strip has the same shape.
    /// </summary>
    public bool HasImage => !string.IsNullOrEmpty(ImageUrl) && !string.IsNullOrEmpty(Url);

    /// <summary>True for a video, which gets a play button over its picture.</summary>
    public bool IsVideo => IconKey == "video";
}

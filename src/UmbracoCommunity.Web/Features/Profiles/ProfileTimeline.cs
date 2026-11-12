using System.Globalization;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// Builds the profile's timeline: every dated item in the activity sections (blog posts, videos, packages,
/// meet-ups, forum and social posts, and activity added by hand), newest first and grouped by month. Talks
/// are not an activity section and stay out of it. A package with no known date is left out, since it can't be
/// placed. Expects a profile that <see cref="ProfileDisplay.Apply"/> has already run on, so hidden sections and
/// items are already gone.
/// </summary>
public static class ProfileTimeline
{
    /// <summary>The query string parameter that selects the type filter, e.g. <c>?type=blog</c>.</summary>
    public const string TypeQueryKey = "type";

    /// <summary>A month heading with a three-letter month name, e.g. "Mar 2026".</summary>
    public static string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The icon (see <c>_KindIcon.cshtml</c>) for an activity section.</summary>
    public static string IconKey(string group) => group switch
    {
        PlatformActivityGroups.Blog => "blog",
        PlatformActivityGroups.Videos => "video",
        PlatformActivityGroups.Packages => "package",
        PlatformActivityGroups.Meetups => "meetup",
        PlatformActivityGroups.Forum => "forum",
        PlatformActivityGroups.Social => "social",
        _ => "other",
    };

    /// <summary>Every dated item in the profile's activity sections, newest first.</summary>
    public static IReadOnlyList<ProfileTimelineEntry> Entries(CommunityProfile profile)
    {
        var entries = new List<ProfileTimelineEntry>();

        entries.AddRange(profile.BlogPosts.Select(p => new ProfileTimelineEntry(
            PlatformActivityGroups.Blog, p.ActivityId, p.Title, p.Url, p.Excerpt, p.PublishedAt, p.Source, null)));

        entries.AddRange(profile.Videos
            .Where(v => v.PublishedAt != null)
            .Select(v => new ProfileTimelineEntry(
                PlatformActivityGroups.Videos, v.ActivityId, v.Title, v.Url, v.Description, v.PublishedAt!.Value, null, v.ThumbnailUrl)));

        entries.AddRange(profile.Packages
            .Where(p => p.Date != null)
            .Select(p => new ProfileTimelineEntry(
                PlatformActivityGroups.Packages,
                p.ActivityId,
                p.Name,
                p.Url,
                p.Description,
                p.Date!.Value,
                p.Versions.Count > 0 ? "v" + p.Versions[0] : null,
                null)));

        entries.AddRange(profile.Meetups.Select(m => new ProfileTimelineEntry(
            PlatformActivityGroups.Meetups,
            m.ActivityId,
            m.EventName,
            m.Url,
            null,
            m.Date,
            string.IsNullOrEmpty(m.Group) ? m.Action : $"{m.Action} · {m.Group}",
            null)));

        entries.AddRange(profile.ForumPosts.Select(p => new ProfileTimelineEntry(
            PlatformActivityGroups.Forum,
            p.ActivityId,
            p.Title,
            p.Url,
            null,
            p.PostedAt,
            p.Replies is { } replies ? $"{replies} replies" : null,
            null)));

        entries.AddRange(profile.SocialPosts.Select(p => new ProfileTimelineEntry(
            PlatformActivityGroups.Social, p.ActivityId, p.Text, p.Url, null, p.PostedAt, p.Platform, null)));

        entries.AddRange(profile.OtherActivities.Select(a => new ProfileTimelineEntry(
            PlatformActivityGroups.Other, a.ActivityId, a.Title, a.Url, a.Description, a.Date, a.Kind, null)));

        // Stable order for items on the same date: the order of the sections above.
        return entries
            .Select((entry, index) => (entry, index))
            .OrderByDescending(e => e.entry.Date)
            .ThenBy(e => e.index)
            .Select(e => e.entry)
            .ToList();
    }

    /// <summary>The entries grouped by calendar month (UTC), newest month first, keeping their order within a month.</summary>
    public static IReadOnlyList<ProfileTimelineMonth> ByMonth(IEnumerable<ProfileTimelineEntry> entries) =>
        entries
            .GroupBy(e => (e.Date.UtcDateTime.Year, e.Date.UtcDateTime.Month))
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g => new ProfileTimelineMonth(
                MonthLabel(g.Key.Year, g.Key.Month),
                $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                g.ToList()))
            .ToList();

    /// <summary>
    /// The type the timeline is filtered by: <paramref name="requested"/> (ignoring case) when it is one of
    /// <paramref name="types"/>, otherwise null, which shows everything.
    /// </summary>
    public static string? SelectedType(string? requested, IReadOnlyList<ProfileTimelineType> types) =>
        string.IsNullOrWhiteSpace(requested)
            ? null
            : types.FirstOrDefault(t => string.Equals(t.Key, requested.Trim(), StringComparison.OrdinalIgnoreCase))?.Key;

    /// <summary>True when <paramref name="entry"/> matches the type filter <paramref name="selectedType"/> (null matches everything).</summary>
    public static bool IsShown(ProfileTimelineEntry entry, string? selectedType) =>
        selectedType == null || entry.Group == selectedType;

    /// <summary>The type filter's options: the sections that have entries, in <see cref="PlatformActivityGroups.All"/> order.</summary>
    public static IReadOnlyList<ProfileTimelineType> Types(IEnumerable<ProfileTimelineEntry> entries)
    {
        var groups = entries.Select(e => e.Group).ToHashSet(StringComparer.Ordinal);
        return PlatformActivityGroups.All
            .Where(groups.Contains)
            .Select(g => new ProfileTimelineType(g, ProfileDisplay.SectionLabel(g)))
            .ToList();
    }
}

/// <summary>One item on the profile's timeline.</summary>
/// <param name="Group">The activity section it comes from (see <see cref="PlatformActivityGroups"/>).</param>
/// <param name="ActivityId">Its activity id, when it has one (what hidden, pinned and no-card items refer to).</param>
/// <param name="Url">Where it links to. Null for a meet-up without a page.</param>
/// <param name="Meta">A short line of context, e.g. the blog it was posted on or the meet-up action.</param>
/// <param name="ImageUrl">A picture already known for it (a video's thumbnail), at its address on its own site; only our stored copy is shown.</param>
public sealed record ProfileTimelineEntry(
    string Group,
    string? ActivityId,
    string Title,
    string? Url,
    string? Description,
    DateTimeOffset Date,
    string? Meta,
    string? ImageUrl)
{
    /// <summary>The preview card to show, or null for a plain line. Set by <see cref="ProfileTimelinePreviewService"/>.</summary>
    public ProfileTimelineCard? Card { get; init; }

    /// <summary>The icon for the entry's section.</summary>
    public string IconKey => ProfileTimeline.IconKey(Group);
}

/// <summary>A preview card for a timeline entry. <paramref name="ImageUrl"/> is always our stored copy (see <see cref="ProfileTimelineImageStore"/>).</summary>
public sealed record ProfileTimelineCard(string ImageUrl, string Title, string? Description);

/// <summary>A month on the timeline, e.g. "Mar 2026", with its entries newest first.</summary>
/// <param name="Key">The month as <c>yyyy-MM</c>, for ids.</param>
public sealed record ProfileTimelineMonth(string Label, string Key, IReadOnlyList<ProfileTimelineEntry> Entries);

/// <summary>An option of the timeline's type filter.</summary>
public sealed record ProfileTimelineType(string Key, string Label);

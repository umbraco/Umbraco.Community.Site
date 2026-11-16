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

    /// <summary>The query string parameter that selects the timeline's page, e.g. <c>?page=2</c>.</summary>
    public const string PageQueryKey = "page";

    /// <summary>How many entries a page of the timeline holds.</summary>
    public const int PageSize = 30;

    /// <summary>
    /// One page of the timeline: the entries that match the type filter (see <see cref="IsShown"/>), newest first, cut into
    /// pages of <see cref="PageSize"/>. <paramref name="requestedPage"/> is the raw <c>?page=</c> value; anything that isn't
    /// a page number, or is past the end, gives the nearest real page (so a bad address shows the first or last page).
    /// </summary>
    public static ProfileTimelinePage Paginate(
        IReadOnlyList<ProfileTimelineEntry> entries, string? selectedType, bool hasTypeFilter, string? requestedPage)
    {
        var matching = entries.Where(e => IsShown(e, selectedType, hasTypeFilter)).ToList();
        var pageCount = Math.Max(1, (matching.Count + PageSize - 1) / PageSize);
        var page = int.TryParse(requestedPage, out var number) ? Math.Clamp(number, 1, pageCount) : 1;
        return new ProfileTimelinePage(matching.Skip((page - 1) * PageSize).Take(PageSize).ToList(), page, pageCount, matching.Count);
    }

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
                p.IsNew ? "New package" : p.Versions.Count > 0 ? "v" + p.Versions[0] : null,
                null)
            {
                IsNewPackage = p.IsNew,
            }));

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
            p.IsSolution ? "Solved" : p.Replies is { } replies ? $"{replies} replies" : null,
            null)
        {
            IsSolution = p.IsSolution,
        }));

        entries.AddRange(MergeCrossPosts(profile.SocialPosts.Select(p => new ProfileTimelineEntry(
            PlatformActivityGroups.Social, p.ActivityId, p.Text, p.Url, null, p.PostedAt, p.Platform, null))));

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

    /// <summary>How far apart two posts can be and still be the same post shared on two platforms.</summary>
    internal static readonly TimeSpan CrossPostWindow = TimeSpan.FromDays(2);

    /// <summary>How alike two posts' texts must be (0 to 1) to count as the same post.</summary>
    internal const double CrossPostSimilarity = 0.8;

    /// <summary>
    /// Folds a post that was shared on more than one platform (e.g. Mastodon and Bluesky) into one entry. Two posts
    /// on different platforms, close in time, whose texts are nearly identical (see <see cref="TextSimilarity"/>) are
    /// the same post (similar as characters or as words): the newest stays, linking to its own post, and its meta lists the platforms ("bluesky, mastodon").
    /// Posts on the same platform are never merged.
    /// </summary>
    internal static IReadOnlyList<ProfileTimelineEntry> MergeCrossPosts(IEnumerable<ProfileTimelineEntry> posts)
    {
        var groups = new List<(ProfileTimelineEntry Lead, string Text, List<string> Platforms)>();
        foreach (var post in posts.OrderByDescending(p => p.Date))
        {
            var platform = post.Meta ?? string.Empty;
            var text = NormaliseSocialText(post.Title);
            var match = groups.FindIndex(g =>
                !g.Platforms.Contains(platform, StringComparer.OrdinalIgnoreCase)
                && (g.Lead.Date - post.Date).Duration() <= CrossPostWindow
                && Math.Max(TextSimilarity(g.Text, text), WordOverlap(g.Text, text)) >= CrossPostSimilarity);

            if (match < 0)
            {
                groups.Add((post, text, [platform]));
            }
            else
            {
                groups[match].Platforms.Add(platform);
            }
        }

        return groups
            .Select(g => g.Platforms.Count == 1
                ? g.Lead
                : g.Lead with { Meta = string.Join(", ", g.Platforms.Where(p => p.Length > 0).Order(StringComparer.OrdinalIgnoreCase)) })
            .ToList();
    }

    /// <summary>Lower-cased words only, without the "Attached: 4 images" note some platforms put in front.</summary>
    private static string NormaliseSocialText(string text)
    {
        var withoutNote = System.Text.RegularExpressions.Regex.Replace(
            text, @"^\s*Attached:\s*\d+\s+images?\s*", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var words = System.Text.RegularExpressions.Regex.Matches(withoutNote.ToLowerInvariant(), @"[\p{L}\p{N}#@]+")
            .Select(m => m.Value);
        return string.Join(' ', words);
    }

    /// <summary>
    /// How many of the words two normalised texts share, from 0 to 1 (the Dice coefficient over their word sets).
    /// Catches the same post with a clause reworded or moved, which an edit distance counts as a big change.
    /// </summary>
    internal static double WordOverlap(string a, string b)
    {
        var first = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var second = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        return first.Count == 0 || second.Count == 0
            ? 0
            : 2.0 * first.Intersect(second).Count() / (first.Count + second.Count);
    }

    /// <summary>
    /// How alike two normalised texts are, from 0 to 1: one minus the Levenshtein edit distance over the longer
    /// length. A text that is a truncated start of the other (ending "...") compares on the shorter length.
    /// </summary>
    internal static double TextSimilarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        // Compare like with like: cut the longer one down to the shorter's length, so a truncated copy still matches.
        var length = Math.Min(a.Length, b.Length);
        if (Math.Max(a.Length, b.Length) > length * 1.25)
        {
            return 0;
        }

        a = a[..length];
        b = b[..length];

        var previous = Enumerable.Range(0, length + 1).ToArray();
        var current = new int[length + 1];
        for (var i = 1; i <= length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return 1.0 - (double)previous[length] / length;
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

    /// <summary>
    /// True when <paramref name="entry"/> matches the type filter <paramref name="selectedType"/>. With no type selected the
    /// timeline shows the highlights: everything except routine package releases (many, and not news), which show
    /// only when "Packages" is selected. A package's first release is a highlight. When <paramref name="hasTypeFilter"/> is false (the member has nothing but package
    /// releases, so there is no filter to pick them with) they show anyway.
    /// </summary>
    public static bool IsShown(ProfileTimelineEntry entry, string? selectedType, bool hasTypeFilter = true) =>
        selectedType == null
            ? !hasTypeFilter || !entry.IsRoutineRelease
            : entry.Group == selectedType;

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

    /// <summary>True for the release that is a package's very first (a brand-new package), which is a highlight; other releases are routine.</summary>
    public bool IsNewPackage { get; init; }

    /// <summary>True for a routine package release: left out of the highlights, shown under "Packages".</summary>
    public bool IsRoutineRelease => Group == PlatformActivityGroups.Packages && !IsNewPackage;

    /// <summary>True for an answer of the member's that was accepted as the solution of a forum topic.</summary>
    public bool IsSolution { get; init; }

    /// <summary>The icon for the entry's section.</summary>
    public string IconKey => IsSolution ? "solved" : ProfileTimeline.IconKey(Group);
}

/// <summary>A preview card for a timeline entry. <paramref name="ImageUrl"/> is always our stored copy (see <see cref="ProfileTimelineImageStore"/>).</summary>
public sealed record ProfileTimelineCard(string ImageUrl, string Title, string? Description);

/// <summary>A month on the timeline, e.g. "Mar 2026", with its entries newest first.</summary>
/// <param name="Key">The month as <c>yyyy-MM</c>, for ids.</param>
public sealed record ProfileTimelineMonth(string Label, string Key, IReadOnlyList<ProfileTimelineEntry> Entries);

/// <summary>An option of the timeline's type filter.</summary>
public sealed record ProfileTimelineType(string Key, string Label);

/// <summary>One page of the timeline: its <paramref name="Entries"/>, which page it is (from 1), how many pages there are, and how many entries match in all.</summary>
public sealed record ProfileTimelinePage(IReadOnlyList<ProfileTimelineEntry> Entries, int Page, int PageCount, int Total);

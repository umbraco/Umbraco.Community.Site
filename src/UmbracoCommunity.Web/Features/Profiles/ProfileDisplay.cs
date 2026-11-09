using System.Diagnostics.CodeAnalysis;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// A member's choices of what the activity sections of their profile show: sections switched off, single
/// items hidden, and up to <see cref="MaxPinnedItems"/> items pinned to the Featured block with a short note
/// on why. Validates the choices a member sends and applies the stored ones to a <see cref="CommunityProfile"/>.
/// The limits are mirrored client side in <c>profile-display.service.ts</c>.
/// </summary>
public static class ProfileDisplay
{
    public const int MaxPinnedItems = 3;

    // One or two lines under the pinned item's title.
    public const int MaxPinReasonLength = 200;

    // The same limit as PlatformProfileId.
    public const int MaxActivityIdLength = 100;

    public const int MaxHiddenItems = 500;

    /// <summary>The heading the profile page uses for an activity section.</summary>
    public static string SectionLabel(string group) => group switch
    {
        PlatformActivityGroups.Blog => "Blog posts",
        PlatformActivityGroups.Forum => "Forum activity",
        PlatformActivityGroups.Packages => "Packages",
        PlatformActivityGroups.Meetups => "Meet-ups",
        PlatformActivityGroups.Videos => "Videos",
        PlatformActivityGroups.Social => "Social activity",
        PlatformActivityGroups.Other => "Other activity",
        _ => group,
    };

    /// <summary>The choices stored on a profile row, with the pins in the member's order. Expects the pins loaded.</summary>
    public static ProfileDisplayChoices StoredChoices(MemberProfileEntity entity) =>
        new(
            entity.HiddenSections,
            entity.HiddenActivityIds,
            entity.PinnedItems
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .Select(p => new ProfilePin(p.ActivityId, p.Reason, StoredSnapshot(p)))
                .ToList());

    /// <summary>
    /// Keeps a copy of each pinned item with its pin, so it can still be featured once it is no longer among the
    /// member's latest activity: taken from <paramref name="activity"/> (the owner's activity, choices not applied)
    /// when the item is there, otherwise carried over from the same pin in <paramref name="stored"/>. A pin that is
    /// in neither is rejected, since there is nothing to show for it. Expects choices from <see cref="TryNormalize"/>.
    /// </summary>
    public static bool TryTakeSnapshots(
        ProfileDisplayChoices choices,
        IEnumerable<ProfileFeaturedItem> activity,
        ProfileDisplayChoices stored,
        [NotNullWhen(true)] out ProfileDisplayChoices? withSnapshots,
        [NotNullWhen(false)] out string? error)
    {
        withSnapshots = null;

        var items = activity
            .DistinctBy(i => i.ActivityId, StringComparer.Ordinal)
            .ToDictionary(i => i.ActivityId, StringComparer.Ordinal);
        var storedPins = stored.Pins
            .DistinctBy(p => p.ActivityId, StringComparer.Ordinal)
            .ToDictionary(p => p.ActivityId, StringComparer.Ordinal);

        var pins = new List<ProfilePin>();
        foreach (var pin in choices.Pins)
        {
            if (items.TryGetValue(pin.ActivityId, out var item))
            {
                pins.Add(pin with { Snapshot = Snapshot(item) });
            }
            else if (storedPins.TryGetValue(pin.ActivityId, out var storedPin))
            {
                // Pins saved before the copy was kept have none; they stay as they are.
                pins.Add(pin with { Snapshot = storedPin.Snapshot });
            }
            else
            {
                error = "That item isn't in your activity.";
                return false;
            }
        }

        withSnapshots = choices with { Pins = pins };
        error = null;
        return true;
    }

    /// <summary>
    /// Checks the member's choices and returns them trimmed, with section keys in their canonical form
    /// and repeated hidden ids removed. Returns false with a message for the member when anything is
    /// out of bounds.
    /// </summary>
    public static bool TryNormalize(
        UpdateProfileDisplayRequest request,
        [NotNullWhen(true)] out ProfileDisplayChoices? choices,
        [NotNullWhen(false)] out string? error)
    {
        choices = null;

        var hiddenSections = new List<string>();
        foreach (var section in request.HiddenSections ?? [])
        {
            var group = PlatformActivityGroups.All.FirstOrDefault(g => string.Equals(g, section?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                error = "That section doesn't exist.";
                return false;
            }

            if (!hiddenSections.Contains(group))
            {
                hiddenSections.Add(group);
            }
        }

        var hiddenIds = new List<string>();
        foreach (var value in request.HiddenItemIds ?? [])
        {
            if (!TryNormalizeId(value, out var id, out error))
            {
                return false;
            }

            if (!hiddenIds.Contains(id, StringComparer.Ordinal))
            {
                hiddenIds.Add(id);
            }
        }

        if (hiddenIds.Count > MaxHiddenItems)
        {
            error = $"You can hide up to {MaxHiddenItems} items.";
            return false;
        }

        var requestedPins = request.Pins ?? [];
        if (requestedPins.Count > MaxPinnedItems)
        {
            error = $"You can pin up to {MaxPinnedItems} items.";
            return false;
        }

        var pins = new List<ProfilePin>();
        foreach (var requestedPin in requestedPins)
        {
            if (!TryNormalizeId(requestedPin?.Id, out var id, out error))
            {
                return false;
            }

            if (pins.Any(p => string.Equals(p.ActivityId, id, StringComparison.Ordinal)))
            {
                error = "An item can only be pinned once.";
                return false;
            }

            if (hiddenIds.Contains(id, StringComparer.Ordinal))
            {
                error = "A hidden item can't be pinned.";
                return false;
            }

            var reason = ProfileText.Normalize(requestedPin?.Reason);
            if (reason is { Length: > MaxPinReasonLength })
            {
                error = $"Why you're proud of it must be {MaxPinReasonLength} characters or fewer.";
                return false;
            }

            pins.Add(new ProfilePin(id, reason));
        }

        choices = new ProfileDisplayChoices(hiddenSections, hiddenIds, pins);
        error = null;
        return true;
    }

    /// <summary>
    /// Applies the profile's <see cref="CommunityProfile.DisplayChoices"/>: switched-off sections are emptied,
    /// hidden items removed, and the pinned items in a shown section are set as
    /// <see cref="CommunityProfile.FeaturedItems"/> in the member's order. A pinned item that is no longer among
    /// the loaded activity is shown from the copy kept with its pin, and skipped when there is none. Talks and
    /// badges are not activity sections, so they are left as they are.
    /// </summary>
    public static CommunityProfile Apply(CommunityProfile profile)
    {
        var choices = profile.DisplayChoices;
        if (choices.HiddenSections.Count == 0 && choices.HiddenActivityIds.Count == 0 && choices.Pins.Count == 0)
        {
            return profile;
        }

        var hiddenSections = new HashSet<string>(choices.HiddenSections, StringComparer.OrdinalIgnoreCase);
        var hiddenIds = new HashSet<string>(choices.HiddenActivityIds, StringComparer.Ordinal);

        IReadOnlyList<T> Shown<T>(string group, IReadOnlyList<T> items, Func<T, string?> activityId) =>
            hiddenSections.Contains(group)
                ? []
                : items.Where(item => activityId(item) is not { } id || !hiddenIds.Contains(id)).ToList();

        var shown = profile with
        {
            BlogPosts = Shown(PlatformActivityGroups.Blog, profile.BlogPosts, p => p.ActivityId),
            ForumPosts = Shown(PlatformActivityGroups.Forum, profile.ForumPosts, p => p.ActivityId),
            Packages = Shown(PlatformActivityGroups.Packages, profile.Packages, p => p.ActivityId),
            Meetups = Shown(PlatformActivityGroups.Meetups, profile.Meetups, m => m.ActivityId),
            Videos = Shown(PlatformActivityGroups.Videos, profile.Videos, v => v.ActivityId),
            SocialPosts = Shown(PlatformActivityGroups.Social, profile.SocialPosts, p => p.ActivityId),
            OtherActivities = Shown(PlatformActivityGroups.Other, profile.OtherActivities, a => a.ActivityId),
        };

        var loadedIds = ActivityItems(profile).Select(i => i.ActivityId).ToHashSet(StringComparer.Ordinal);
        var shownItems = ActivityItems(shown)
            .DistinctBy(i => i.ActivityId, StringComparer.Ordinal)
            .ToDictionary(i => i.ActivityId, StringComparer.Ordinal);

        // The loaded item when it is there, so it is up to date; otherwise the copy kept with the pin.
        ProfileFeaturedItem? Featured(ProfilePin pin)
        {
            if (loadedIds.Contains(pin.ActivityId))
            {
                return shownItems.GetValueOrDefault(pin.ActivityId);
            }

            return pin.Snapshot is { } snapshot && !hiddenSections.Contains(snapshot.Group) && !hiddenIds.Contains(pin.ActivityId)
                ? snapshot
                : null;
        }

        return shown with
        {
            FeaturedItems = choices.Pins
                .Select(pin => Featured(pin) is { } item ? item with { Reason = pin.Reason } : null)
                .OfType<ProfileFeaturedItem>()
                .ToList(),
        };
    }

    /// <summary>
    /// Every item in the profile's activity sections that has an activity id (and so can be hidden or pinned),
    /// section by section in <see cref="PlatformActivityGroups.All"/> order.
    /// </summary>
    public static IEnumerable<ProfileFeaturedItem> ActivityItems(CommunityProfile profile)
    {
        foreach (var group in PlatformActivityGroups.All)
        {
            IEnumerable<ProfileFeaturedItem> items = group switch
            {
                PlatformActivityGroups.Blog => profile.BlogPosts
                    .Where(p => p.ActivityId != null)
                    .Select(p => new ProfileFeaturedItem(group, p.ActivityId!, p.Title, p.Url, p.Excerpt, p.PublishedAt, p.Source, null)),
                PlatformActivityGroups.Forum => profile.ForumPosts
                    .Where(p => p.ActivityId != null)
                    .Select(p => new ProfileFeaturedItem(group, p.ActivityId!, p.Title, p.Url, null, p.PostedAt, null, null)),
                PlatformActivityGroups.Packages => profile.Packages
                    .Where(p => p.ActivityId != null)
                    .Select(p => new ProfileFeaturedItem(group, p.ActivityId!, p.Name, p.Url, p.Description, null, null, null)),
                PlatformActivityGroups.Meetups => profile.Meetups
                    .Where(m => m.ActivityId != null && m.Url != null)
                    .Select(m => new ProfileFeaturedItem(group, m.ActivityId!, m.EventName, m.Url!, null, m.Date, m.Action, null)),
                PlatformActivityGroups.Videos => profile.Videos
                    .Where(v => v.ActivityId != null)
                    .Select(v => new ProfileFeaturedItem(group, v.ActivityId!, v.Title, v.Url, v.Description, v.PublishedAt, null, null)),
                PlatformActivityGroups.Social => profile.SocialPosts
                    .Where(p => p.ActivityId != null)
                    .Select(p => new ProfileFeaturedItem(group, p.ActivityId!, p.Text, p.Url, null, p.PostedAt, p.Platform, null)),
                PlatformActivityGroups.Other => profile.OtherActivities
                    .Where(a => a.ActivityId != null)
                    .Select(a => new ProfileFeaturedItem(group, a.ActivityId!, a.Title, a.Url, a.Description, a.Date, a.Kind, null)),
                _ => [],
            };

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    private static bool TryNormalizeId(
        string? value,
        [NotNullWhen(true)] out string? id,
        [NotNullWhen(false)] out string? error)
    {
        id = ProfileText.Normalize(value);
        if (id == null)
        {
            error = "An item id is missing.";
            return false;
        }

        if (id.Length > MaxActivityIdLength)
        {
            id = null;
            error = $"Item ids must be {MaxActivityIdLength} characters or fewer.";
            return false;
        }

        error = null;
        return true;
    }

    private static ProfileFeaturedItem? StoredSnapshot(MemberPinnedItemEntity pin) =>
        pin is { Group: not null, Title: not null, Url: not null }
            ? new ProfileFeaturedItem(
                pin.Group,
                pin.ActivityId,
                pin.Title,
                pin.Url,
                pin.Description,
                pin.DateUtc is { } date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)) : null,
                pin.Source,
                null)
            : null;

    /// <summary>The item cut to fit what a pin stores, or null when its address is too long to keep.</summary>
    private static ProfileFeaturedItem? Snapshot(ProfileFeaturedItem item)
    {
        if (item.Url.Length > MemberPinnedItemEntity.MaxUrlLength || item.Group.Length > MemberPinnedItemEntity.MaxGroupLength)
        {
            return null;
        }

        return item with
        {
            Title = Shorten(item.Title, MemberPinnedItemEntity.MaxTitleLength)!,
            Description = Shorten(item.Description, MemberPinnedItemEntity.MaxDescriptionLength),
            Source = Shorten(item.Source, MemberPinnedItemEntity.MaxSourceLength),
            Reason = null,
        };
    }

    private static string? Shorten(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..(maxLength - 1)].TrimEnd() + "…" : value;
}

using System.Diagnostics.CodeAnalysis;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using UmbracoCommunity.Web.Features.Profiles.Models;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// The rules for activity a member adds to their profile by hand: which kinds there are, the limits on what they
/// type, and the id the entry has once it is part of the activity sections. The limits are mirrored client side
/// in <c>profile-activity.service.ts</c>.
/// </summary>
public static class ProfileActivityEntry
{
    public const string Blog = "blog";
    public const string Video = "videos";
    public const string Package = "packages";
    public const string Event = "event";
    public const string Talk = "talk";
    public const string Contribution = "contribution";
    public const string Podcast = "podcast";
    public const string Other = "other";

    /// <summary>What a hand-added entry can be, in the order members are offered them.</summary>
    public static readonly IReadOnlyList<string> Kinds = [Blog, Video, Package, Event, Talk, Contribution, Podcast, Other];

    /// <summary>How many entries a member can have waiting for a check at once.</summary>
    public const int MaxPendingEntries = 10;

    public const int MaxTitleLength = MemberActivityEntryEntity.MaxTitleLength;
    public const int MaxDescriptionLength = MemberActivityEntryEntity.MaxDescriptionLength;
    public const int MaxReasonLength = MemberActivityEntryEntity.MaxReasonLength;

    private static readonly DateOnly EarliestDate = new(1990, 1, 1);

    /// <summary>What the member is offered for the kind, and what the profile calls it.</summary>
    public static string KindLabel(string kind) => kind switch
    {
        Blog => "Blog post",
        Video => "Video",
        Package => "Package",
        Event => "Event",
        Talk => "Talk",
        Contribution => "Open-source contribution",
        Podcast => "Podcast or interview",
        Other => "Other",
        _ => kind,
    };

    /// <summary>The activity section an entry of the kind is shown in.</summary>
    public static string Group(string kind) => kind switch
    {
        Blog => PlatformActivityGroups.Blog,
        Video => PlatformActivityGroups.Videos,
        Package => PlatformActivityGroups.Packages,
        Event => PlatformActivityGroups.Meetups,
        _ => PlatformActivityGroups.Other,
    };

    /// <summary>The id the entry has in the profile's activity sections, so it can be hidden or pinned like the rest.</summary>
    public static string ActivityId(int entryId) => $"added:{entryId}";

    /// <summary>
    /// Checks what the member typed and returns it trimmed and with the kind in its canonical form. Returns
    /// false with a message for the member when anything is out of bounds.
    /// </summary>
    public static bool TryNormalize(
        AddActivityEntryRequest request,
        DateOnly today,
        [NotNullWhen(true)] out AddActivityEntryRequest? normalized,
        [NotNullWhen(false)] out string? error)
    {
        normalized = null;

        var kind = Kinds.FirstOrDefault(k => string.Equals(k, request.Kind?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (kind == null)
        {
            error = "Choose what kind of activity this is.";
            return false;
        }

        var title = ProfileText.Normalize(request.Title);
        if (title == null || title.Length > MaxTitleLength)
        {
            error = $"The title must be 1 to {MaxTitleLength} characters.";
            return false;
        }

        if (!ProfileLinkUrl.TryParse(request.Url, out var url) || url.AbsoluteUri.Length > ProfileLinkUrl.MaxLength)
        {
            error = "Enter a link that starts with https://.";
            return false;
        }

        if (request.Date is not { } date || date < EarliestDate || date > today)
        {
            error = "Enter the date, not in the future.";
            return false;
        }

        var description = ProfileText.Normalize(request.Description);
        if (description is { Length: > MaxDescriptionLength })
        {
            error = $"The description can be up to {MaxDescriptionLength} characters.";
            return false;
        }

        normalized = new AddActivityEntryRequest(kind, title, url.AbsoluteUri, date, description);
        error = null;
        return true;
    }
}

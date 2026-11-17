using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>A piece of activity the member wants to add to their profile. <c>Kind</c> is one of <see cref="ProfileActivityEntry.Kinds"/>.</summary>
public sealed record AddActivityEntryRequest(string? Kind, string? Title, string? Url, DateOnly? Date, string? Description);

/// <summary>
/// An activity entry a member added by hand, with who it belongs to and where it stands. <c>ImagePath</c> is where the
/// picture the member attached is kept (see <see cref="ProfileActivityImageStore"/>), or null when there is none; its
/// address comes from <see cref="ProfileActivityEntryService.ImageUrl"/>.
/// </summary>
public sealed record ActivityEntryRecord(
    int Id,
    string GitHubHandle,
    string DisplayName,
    string Kind,
    string Title,
    string Url,
    DateOnly Date,
    string? Description,
    ActivityEntryState State,
    string? RejectionReason,
    string? DecidedByUserName,
    DateTime? DecidedUtc,
    DateTime CreatedUtc,
    string? ImagePath = null);

public sealed record ActivityEntryPage(IReadOnlyList<ActivityEntryRecord> Items, int Total);

/// <summary>How adding or editing an entry went. <see cref="ProfileActivityEntry.ChangeError"/> words it for the member.</summary>
public enum ActivityEntryChangeOutcome
{
    Saved,

    /// <summary>The member has no such entry, or it was withdrawn.</summary>
    NotFound,

    /// <summary>The member already has another entry with the same link.</summary>
    DuplicateLink,

    /// <summary>A moderator removed an entry of the member's with the same link, so it can't come back by being added again.</summary>
    RemovedLink,

    /// <summary>A moderator removed the entry being edited; it can be withdrawn, not changed.</summary>
    RemovedByModerator,
}

/// <summary>The outcome of adding or editing an entry, and the entry as stored when it was <see cref="ActivityEntryChangeOutcome.Saved"/>.</summary>
public sealed record ActivityEntryChange(ActivityEntryChangeOutcome Outcome, ActivityEntryRecord? Entry = null)
{
    public bool Saved => Outcome == ActivityEntryChangeOutcome.Saved && Entry != null;
}

/// <summary>
/// What an edit does to the entry's picture: keeps it, removes it, or replaces it with one already stored at
/// <see cref="NewPath"/> (from <see cref="ProfileActivityImageStore.SaveAsync"/>).
/// </summary>
public sealed record ActivityEntryImageChange(string? NewPath, bool Remove)
{
    public static readonly ActivityEntryImageChange Keep = new(null, false);

    public static readonly ActivityEntryImageChange RemoveImage = new(null, true);

    public static ActivityEntryImageChange Replace(string path) => new(path, false);
}

/// <summary>An activity entry as the member sees it on their account page. <c>ImageUrl</c> is the attached picture, on this site.</summary>
public sealed record MemberActivityEntryResponse(
    int Id,
    string Kind,
    string KindLabel,
    string Title,
    string Url,
    DateOnly Date,
    string? Description,
    string State,
    string? RejectionReason,
    string? ImageUrl,
    string? PageImageUrl = null);

/// <summary>The link a member wants the details of, before adding it.</summary>
public sealed record LookUpActivityLinkRequest(string? Url);

/// <summary>
/// What the page behind a link offers for a new entry (see <see cref="ProfileActivityLinkLookupService"/>); each part is
/// null when the page didn't say. <c>Kind</c> is a guess, one of <see cref="ProfileActivityEntry.Kinds"/>. <c>ImageUrl</c> is
/// our copy of the page's picture, served from this site; the entry's timeline card uses that picture when the member
/// attaches none of their own.
/// </summary>
public sealed record ActivityLinkDetails(string? Kind, string? Title, DateOnly? Date, string? Description, string? ImageUrl)
{
    public static readonly ActivityLinkDetails Empty = new(null, null, null, null, null);
}

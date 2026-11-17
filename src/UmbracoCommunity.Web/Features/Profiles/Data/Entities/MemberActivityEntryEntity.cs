namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

/// <summary>
/// Where a hand-added entry stands. The names date from when entries were checked before they showed; entries are now
/// shown at once, so the values are kept (they are stored) and read as below.
/// </summary>
public enum ActivityEntryState
{
    /// <summary>
    /// Legacy: added while entries still waited for a check, and not decided yet. Not shown; a moderator can still show
    /// or remove it. New entries never get this state.
    /// </summary>
    Pending = 0,

    /// <summary>Shown on the member's profile: every new entry, and a waiting or removed one a moderator showed.</summary>
    Verified = 1,

    /// <summary>Removed from the profile by a moderator, with a note for the member (<see cref="MemberActivityEntryEntity.RejectionReason"/>).</summary>
    Rejected = 2,

    /// <summary>Withdrawn by the member. One that a moderator had removed keeps the note.</summary>
    Withdrawn = 3
}

/// <summary>
/// A piece of activity a member added by hand. It shows on their profile as soon as it is added (see
/// <see cref="ActivityEntryState"/>); a moderator can remove it afterwards. <see cref="Kind"/> is one of
/// <see cref="ProfileActivityEntry.Kinds"/>.
/// </summary>
public class MemberActivityEntryEntity
{
    public const int MaxKindLength = 50;
    public const int MaxTitleLength = 200;
    public const int MaxUrlLength = 2048;
    public const int MaxDescriptionLength = 500;
    public const int MaxReasonLength = 500;
    public const int MaxImagePathLength = 300;

    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    public MemberProfileEntity? MemberProfile { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public DateTime DateUtc { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Where the picture the member attached is kept in the media file system (see <see cref="ProfileActivityImageStore"/>),
    /// or null when there is none. Cleared when the entry is withdrawn, as the file is then deleted.
    /// </summary>
    public string? ImagePath { get; set; }

    public ActivityEntryState State { get; set; }

    /// <summary>
    /// The moderator's note to the member when the entry was removed. Kept when the member then withdraws it, so its
    /// link can't simply be added again.
    /// </summary>
    public string? RejectionReason { get; set; }

    public Guid? DecidedByUserKey { get; set; }

    public string? DecidedByUserName { get; set; }

    public DateTime? DecidedUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

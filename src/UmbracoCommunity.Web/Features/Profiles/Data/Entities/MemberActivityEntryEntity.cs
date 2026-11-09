namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

public enum ActivityEntryState
{
    /// <summary>Waiting for an admin to check it.</summary>
    Pending = 0,
    Verified = 1,
    Rejected = 2,
    Withdrawn = 3
}

/// <summary>
/// A piece of activity a member added by hand. It is shown on their profile once an admin has verified it.
/// <see cref="Kind"/> is one of <see cref="ProfileActivityEntry.Kinds"/>.
/// </summary>
public class MemberActivityEntryEntity
{
    public const int MaxKindLength = 50;
    public const int MaxTitleLength = 200;
    public const int MaxUrlLength = 2048;
    public const int MaxDescriptionLength = 500;
    public const int MaxReasonLength = 500;

    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    public MemberProfileEntity? MemberProfile { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public DateTime DateUtc { get; set; }

    public string? Description { get; set; }

    public ActivityEntryState State { get; set; }

    /// <summary>The admin's note to the member when the entry was rejected.</summary>
    public string? RejectionReason { get; set; }

    public Guid? DecidedByUserKey { get; set; }

    public string? DecidedByUserName { get; set; }

    public DateTime? DecidedUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

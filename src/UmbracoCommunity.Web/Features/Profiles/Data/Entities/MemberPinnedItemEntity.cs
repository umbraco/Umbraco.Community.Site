namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

/// <summary>
/// An activity the member has pinned to the Featured block of their profile, with their own
/// short note on why. A member has at most <see cref="ProfileDisplay.MaxPinnedItems"/>, shown in
/// <see cref="SortOrder"/>.
/// <para>The item itself (<see cref="Group"/> to <see cref="Source"/>) is copied in when it is pinned, so it
/// can still be shown once it is no longer among the member's latest activity. Pins saved before the copy
/// was kept have none (<see cref="Title"/> is null).</para>
/// </summary>
public class MemberPinnedItemEntity
{
    public const int MaxGroupLength = 50;
    public const int MaxTitleLength = 500;
    public const int MaxUrlLength = 2048;
    public const int MaxDescriptionLength = 500;
    public const int MaxSourceLength = 100;

    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    public MemberProfileEntity? MemberProfile { get; set; }

    /// <summary>The id of the activity on the external content platform.</summary>
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>The member's note on why they are proud of it, as plain text. Null = no note.</summary>
    public string? Reason { get; set; }

    public int SortOrder { get; set; }

    /// <summary>The activity section the item comes from (see <see cref="Models.PlatformActivityGroups"/>).</summary>
    public string? Group { get; set; }

    public string? Title { get; set; }

    public string? Url { get; set; }

    public string? Description { get; set; }

    public DateTime? DateUtc { get; set; }

    public string? Source { get; set; }
}

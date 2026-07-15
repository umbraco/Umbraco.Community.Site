namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

public enum SphereFeedSyncStatus
{
    Pending = 0,
    Synced = 1,
    Failed = 2
}

/// <summary>
/// Where a feed came from. Sphere has no profile/feed API yet, so every feed is
/// <see cref="Member"/>-added today — this exists so the UI can already distinguish them
/// once Sphere-supplied feeds are a real possibility (see <see cref="MemberFeedEntity.Source"/>).
/// </summary>
public enum FeedSource
{
    Member = 0,
    Sphere = 1
}

/// <summary>
/// An external feed (blog, LinkedIn, Mastodon, etc.) a member has associated with their
/// profile. Never hard-deleted once removed — <see cref="IsRemoved"/>/<see cref="RemovedReason"/>
/// keep a soft-delete audit trail instead, distinct from <see cref="IsHidden"/> (still
/// manageable, just excluded from public display).
/// </summary>
public class MemberFeedEntity
{
    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    public MemberProfileEntity? MemberProfile { get; set; }

    /// <summary>Free text (e.g. "LinkedIn", "Blog", "Mastodon") — matches ProfileSocialLink.Platform's open-ended shape.</summary>
    public string Platform { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public FeedSource Source { get; set; } = FeedSource.Member;

    /// <summary>Hidden feeds stay manageable but are excluded from the public profile.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Soft-delete flag. A removed feed is never hard-deleted, so the reason stays auditable.</summary>
    public bool IsRemoved { get; set; }

    public string? RemovedReason { get; set; }

    public DateTime? RemovedUtc { get; set; }

    public DateTime AddedUtc { get; set; }

    /// <summary>
    /// Lightweight sync-state tracking so a future reconciliation job can find feeds that
    /// never made it to Sphere, without needing a full delivery/tracking pipeline today.
    /// </summary>
    public SphereFeedSyncStatus SphereSyncStatus { get; set; } = SphereFeedSyncStatus.Pending;

    public DateTime? LastSphereSyncAttemptUtc { get; set; }

    public string? LastSphereSyncError { get; set; }
}

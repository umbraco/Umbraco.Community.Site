namespace UmbracoCommunity.BlogAnnouncements.Models.Entities;

/// <summary>Lifecycle state of a tracked blog post in the announcement pipeline.</summary>
public enum AnnouncementStatus : byte
{
    /// <summary>Awaiting delivery — new-within-window, or behind the per-cycle cap.</summary>
    Pending = 0,

    /// <summary>Delivery to Discord confirmed.</summary>
    Announced = 1,

    /// <summary>Never-seen but published outside the recency window; deliberately not announced.</summary>
    SkippedTooOld = 2,

    /// <summary>Manually marked "do not announce" (spam / wrong language / slipped-fingerprint dup).</summary>
    Suppressed = 3,

    /// <summary>A delivery attempt failed; retried on the next cycle.</summary>
    Failed = 4,

    /// <summary>
    /// Delivery is in flight: a cycle has claimed this row and committed the claim before sending,
    /// so a concurrent cycle (a second app instance, or an overlapping schedule) can't send the
    /// same post again. Cleared to the delivery outcome when the send settles, or reverted to
    /// <see cref="Failed"/> by the next cycle if the claim goes stale (process died mid-send).
    /// </summary>
    Claimed = 5,
}

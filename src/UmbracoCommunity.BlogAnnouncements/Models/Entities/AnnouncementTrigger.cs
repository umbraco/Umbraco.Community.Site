namespace UmbracoCommunity.BlogAnnouncements.Models.Entities;

/// <summary>What caused a delivery attempt.</summary>
public enum AnnouncementTrigger : byte
{
    /// <summary>Fired automatically by the detection cycle.</summary>
    Auto = 0,

    /// <summary>Re-posted by hand from the dashboard.</summary>
    Repost = 1,

    /// <summary>Posted by hand for a previously un-announced post (Pending / SkippedTooOld).</summary>
    PostNow = 2,

    /// <summary>Posted while working through the 30-day backfill list.</summary>
    Backfill = 3,

    /// <summary>
    /// Not a delivery: the post was manually reset to "not announced" from the dashboard so the
    /// automatic cycle can pick it up again. Recorded in the attempt history for auditability.
    /// </summary>
    Reset = 4,
}

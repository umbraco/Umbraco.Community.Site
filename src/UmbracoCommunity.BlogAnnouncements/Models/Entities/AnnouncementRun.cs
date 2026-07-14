namespace UmbracoCommunity.BlogAnnouncements.Models.Entities;

/// <summary>One detection cycle — a heartbeat row for the "is the pipeline alive?" view.</summary>
public class AnnouncementRun
{
    public int Id { get; set; }
    public DateTime RunUtc { get; set; }

    /// <summary>Posts returned by the fresh Sphere fetch this cycle.</summary>
    public int Fetched { get; set; }

    /// <summary>Never-seen posts recorded this cycle (announced + skipped-too-old).</summary>
    public int New { get; set; }

    /// <summary>Posts whose delivery succeeded this cycle.</summary>
    public int Announced { get; set; }

    /// <summary>Never-seen posts recorded as SkippedTooOld this cycle.</summary>
    public int Skipped { get; set; }

    /// <summary>Delivery attempts that failed this cycle.</summary>
    public int Failed { get; set; }

    public bool DryRun { get; set; }
}

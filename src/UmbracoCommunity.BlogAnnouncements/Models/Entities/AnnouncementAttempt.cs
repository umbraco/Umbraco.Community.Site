namespace UmbracoCommunity.BlogAnnouncements.Models.Entities;

/// <summary>One delivery attempt against a destination for a tracked post.</summary>
public class AnnouncementAttempt
{
    public int Id { get; set; }

    /// <summary>FK to <see cref="AnnouncedBlogPost.SphereId"/>.</summary>
    public Guid SphereId { get; set; }

    public DateTime AttemptedUtc { get; set; }

    /// <summary>Free-form outcome: <c>Success</c>, <c>Failed</c>, or <c>DryRun</c>.</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>HTTP status from the destination, when a request was actually made.</summary>
    public int? HttpStatus { get; set; }

    public AnnouncementTrigger Trigger { get; set; } = AnnouncementTrigger.Auto;

    /// <summary>Delivery destination. Defaulted to "Discord" so more destinations can be added later without a schema change.</summary>
    public string Destination { get; set; } = "Discord";

    public AnnouncedBlogPost? Post { get; set; }
}

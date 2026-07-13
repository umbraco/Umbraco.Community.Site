using UmbracoCommunity.BlogAnnouncements.Models.Entities;

namespace UmbracoCommunity.BlogAnnouncements.Api.Models;

/// <summary>A single row in the Posts tab table.</summary>
public sealed class PostListItem
{
    public Guid SphereId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? AuthorName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public string? AuthorProfileUrl { get; init; }
    public DateTime PublishedAtUtc { get; init; }
    public DateTime? AnnouncedUtc { get; init; }

    /// <summary><see cref="AnnouncementStatus"/> as a byte for the client.</summary>
    public byte Status { get; init; }

    public int AttemptCount { get; init; }
}

/// <summary>Paged response for the Posts tab.</summary>
public sealed class PostListResponse
{
    public int Total { get; init; }
    public IReadOnlyList<PostListItem> Items { get; init; } = [];
}

/// <summary>One delivery attempt, for the details modal history list.</summary>
public sealed class AttemptItem
{
    public int Id { get; init; }
    public DateTime AttemptedUtc { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public int? HttpStatus { get; init; }

    /// <summary><see cref="AnnouncementTrigger"/> as a byte for the client.</summary>
    public byte Trigger { get; init; }

    public string Destination { get; init; } = string.Empty;
}

/// <summary>Full details for one tracked post plus its attempt history.</summary>
public sealed class PostDetail
{
    public Guid SphereId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Excerpt { get; init; }
    public string? AuthorName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public string? AuthorProfileUrl { get; init; }
    public string? CoverImageUrl { get; init; }
    public DateTime PublishedAtUtc { get; init; }
    public DateTime FirstSeenUtc { get; init; }
    public DateTime? AnnouncedUtc { get; init; }
    public string Fingerprint { get; init; } = string.Empty;
    public byte Status { get; init; }
    public IReadOnlyList<AttemptItem> Attempts { get; init; } = [];
}

/// <summary>One detection-cycle heartbeat row for the Runs tab.</summary>
public sealed class RunListItem
{
    public int Id { get; init; }
    public DateTime RunUtc { get; init; }
    public int Fetched { get; init; }
    public int New { get; init; }
    public int Announced { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public bool DryRun { get; init; }
}

/// <summary>Paged response for the Runs tab.</summary>
public sealed class RunListResponse
{
    public int Total { get; init; }
    public IReadOnlyList<RunListItem> Items { get; init; } = [];
}

/// <summary>Read-only effective config snapshot for the Settings tab. Never exposes the webhook URL.</summary>
public sealed class SettingsResponse
{
    public int RecencyWindowDays { get; init; }
    public int MaxAnnouncementsPerCycle { get; init; }
    public bool DryRun { get; init; }
    public bool WebhookConfigured { get; init; }
}

/// <summary>Body of the announce (repost / post-now) request.</summary>
public sealed class AnnounceRequest
{
    /// <summary>The manual trigger reason: <c>Repost</c> or <c>PostNow</c>.</summary>
    public string Trigger { get; init; } = string.Empty;
}

/// <summary>Result of a manual announce or a test message.</summary>
public sealed class DeliveryResultResponse
{
    /// <summary><c>Success</c>, <c>Failed</c>, or <c>DryRun</c>.</summary>
    public string Outcome { get; init; } = string.Empty;
    public int? HttpStatus { get; init; }
    public bool DryRun { get; init; }

    /// <summary>The post's status after the attempt (absent for a test message).</summary>
    public byte? Status { get; init; }
    public DateTime? AnnouncedUtc { get; init; }
}

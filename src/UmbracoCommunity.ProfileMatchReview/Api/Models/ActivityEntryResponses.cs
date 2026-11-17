namespace UmbracoCommunity.ProfileMatchReview.Api.Models;

/// <summary>An activity entry for moderation. <c>ImageUrl</c> is the picture the member attached (served from the site), or null.</summary>
public sealed record ActivityEntryResponse(
    int Id,
    string Handle,
    string DisplayName,
    string Kind,
    string KindLabel,
    string Title,
    string Url,
    DateOnly Date,
    string? Description,
    string State,
    string? RejectionReason,
    string? DecidedByUserName,
    DateTime? DecidedUtc,
    DateTime CreatedUtc,
    string? ImageUrl);

public sealed record ActivityEntryListResponse(IReadOnlyList<ActivityEntryResponse> Items, int Total);

public sealed record RejectActivityEntryRequest(string? Reason);

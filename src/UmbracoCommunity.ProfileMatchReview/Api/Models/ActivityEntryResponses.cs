namespace UmbracoCommunity.ProfileMatchReview.Api.Models;

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
    DateTime CreatedUtc);

public sealed record ActivityEntryListResponse(IReadOnlyList<ActivityEntryResponse> Items, int Total);

public sealed record RejectActivityEntryRequest(string? Reason);

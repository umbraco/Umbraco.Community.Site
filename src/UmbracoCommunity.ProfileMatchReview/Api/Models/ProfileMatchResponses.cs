namespace UmbracoCommunity.ProfileMatchReview.Api.Models;

public sealed record MatchSignalResponse(string Name, string Detail);

public sealed record ProfileMatchResponse(
    int Id,
    string Handle,
    string DisplayName,
    string SourceUrl,
    string State,
    double Confidence,
    IReadOnlyList<MatchSignalResponse> Signals,
    string DecidedBy,
    string? DecidedByUserName,
    DateTime DecidedUtc);

public sealed record ProfileMatchListResponse(IReadOnlyList<ProfileMatchResponse> Items, int Total);

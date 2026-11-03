using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles.Matching;

/// <summary>A stored match decision together with the member it belongs to.</summary>
public sealed record ProfileMatchRecord(
    int Id,
    string GitHubHandle,
    string DisplayName,
    string SourceUrl,
    ProfileMatchState State,
    double Confidence,
    IReadOnlyList<MatchSignal> Signals,
    ProfileMatchDecider DecidedBy,
    string? DecidedByUserName,
    DateTime DecidedUtc);

namespace UmbracoCommunity.Web.Features.Profiles.Matching;

/// <summary>One piece of evidence behind a match decision, shown to the reviewer.</summary>
public sealed record MatchSignal(string Name, string Detail);

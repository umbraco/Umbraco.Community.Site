namespace UmbracoCommunity.Web.Features.Profiles.Matching;

public sealed record ProfileMatchPage(IReadOnlyList<ProfileMatchRecord> Items, int Total);

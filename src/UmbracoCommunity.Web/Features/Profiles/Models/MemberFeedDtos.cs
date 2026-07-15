namespace UmbracoCommunity.Web.Features.Profiles.Models;

public sealed record MemberFeedResponse(int Id, string Platform, string Url, bool IsHidden, string Source);

public sealed record AddFeedRequest(string Platform, string Url);

public sealed record RemoveFeedRequest(string? Reason);

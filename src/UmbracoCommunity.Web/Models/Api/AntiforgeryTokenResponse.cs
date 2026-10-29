namespace UmbracoCommunity.Web.Models.Api;

/// <summary>An antiforgery request token and the header it must be sent in.</summary>
public sealed record AntiforgeryTokenResponse(string Token, string HeaderName);

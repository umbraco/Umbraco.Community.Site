namespace UmbracoCommunity.Web.Features.Mvp.Models;

public sealed record MvpMember(
    string Name,
    bool IsRenewal,
    string AvatarUrl,
    string? AvatarSrcset,
    string? GitHubHandle);

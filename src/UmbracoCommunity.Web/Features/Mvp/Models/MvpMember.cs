namespace UmbracoCommunity.Web.Features.Mvp.Models;

public sealed record MvpMember(
    int Id,
    string Name,
    bool IsRenewal,
    string AvatarUrl,
    string? AvatarSrcset,
    string? GitHubHandle);

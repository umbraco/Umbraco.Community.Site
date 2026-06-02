namespace UmbracoCommunity.Web.Features.Mvp.Models;

public sealed record MvpYear(int Year, IReadOnlyList<MvpMember> Members);

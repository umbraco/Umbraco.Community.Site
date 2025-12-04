namespace UmbracoCommunity.Web.Features.GitHubUsers.Api.Models;

public class ReleaseSummary
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<ReleaseInfo> Releases { get; set; } = new();
}
namespace UmbracoCommunity.Extensions.Models;

public class ReleaseInfo
{
    public string Version { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public bool IsLts { get; set; }
    public bool IsMajor { get; set; }
    public bool IsPreRelease { get; set; }
    public string Url { get; set; } = string.Empty;
    public int TotalPullRequests { get; set; }
    public int ExternalPullRequests { get; set; }
    public int ExternalContributors { get; set; }
    public List<ContributorDetail> TopContributors { get; set; } = new();
}

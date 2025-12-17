namespace UmbracoCommunity.Extensions.Models;

public class ContributionStats
{
    public int TotalExternalPullRequests { get; set; }
    public int TotalExternalContributors { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<ContributorDetail> TopContributors { get; set; } = new();
}

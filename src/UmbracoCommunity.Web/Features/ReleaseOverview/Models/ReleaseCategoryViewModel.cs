namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseCategoryViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public List<ReleasePullRequestViewModel> PullRequests { get; set; } = new();
    public int Count => PullRequests.Count;
}

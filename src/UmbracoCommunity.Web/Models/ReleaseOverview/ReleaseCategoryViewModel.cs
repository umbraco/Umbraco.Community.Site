namespace UmbracoCommunity.Web.Models.ReleaseOverview;

public class ReleaseCategoryViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public List<ReleasePullRequestViewModel> PullRequests { get; set; } = new();
    public int Count => PullRequests.Count;
}

namespace UmbracoCommunity.Web.Models.Releases;

public class ReleaseCategoryViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public List<ReleasePullRequestViewModel> PullRequests { get; set; } = new();
    public int Count => PullRequests.Count;
}

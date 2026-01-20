namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseGroupViewModel
{
    public string ReleaseLabel { get; set; } = string.Empty;
    public string GitHubLabel { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public List<ReleasePullRequestViewModel> PullRequests { get; set; } = new();
    public List<ReleaseCategoryViewModel> Categories { get; set; } = new();
    public int TotalCount => PullRequests.Count;
    public int CommunityCount => PullRequests.Count(pr => !pr.IsHqMember && !pr.IsIssue);
    public int HqCount => PullRequests.Count(pr => pr.IsHqMember && !pr.IsIssue);
    public int UniqueCommunityContributorsCount => PullRequests.Where(pr => !pr.IsHqMember && !pr.IsIssue).Select(pr => pr.AuthorLogin).Distinct().Count();
    public List<(string Name, string Login)> FirstTimeContributors => PullRequests
        .Where(pr => !pr.IsHqMember && pr.IsFirstTimeContributor && !pr.IsIssue)
        .GroupBy(pr => pr.AuthorLogin)
        .Select(g => (Name: g.First().AuthorName ?? g.First().AuthorLogin, Login: g.First().AuthorLogin))
        .OrderBy(c => c.Name)
        .ToList();
    public int FirstTimeContributorsCount => FirstTimeContributors.Count;
}
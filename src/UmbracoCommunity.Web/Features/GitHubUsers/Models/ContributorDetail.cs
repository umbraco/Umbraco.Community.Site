namespace UmbracoCommunity.Web.Features.GitHubUsers.Api.Models;

public class ContributorDetail
{
    public string Login { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PullRequestCount { get; set; }
}
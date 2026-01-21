using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Extensions.Features.GitHub.Models;

public class GitHubDataExport
{
    public List<GitHubIssue> Issues { get; set; } = new();
    public List<GitHubPullRequest> PullRequests { get; set; } = new();
    public List<GitHubDiscussion> Discussions { get; set; } = new();
    public Dictionary<string, Dictionary<string, DateTime>> NuGetPackages { get; set; } = new();
}

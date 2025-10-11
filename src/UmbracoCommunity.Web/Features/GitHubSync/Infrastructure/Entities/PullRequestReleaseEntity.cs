namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure.Entities;

public class PullRequestReleaseEntity
{
    public string PullRequestId { get; set; } = string.Empty;
    public string ReleaseLabel { get; set; } = string.Empty;
}

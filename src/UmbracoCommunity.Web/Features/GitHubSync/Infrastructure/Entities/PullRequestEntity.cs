namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure.Entities;

public class PullRequestEntity
{
    public string Id { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public int Number { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Data { get; set; } = string.Empty; // JSON
}

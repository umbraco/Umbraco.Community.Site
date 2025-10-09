namespace UmbracoCommunity.Web.Features.GitHubSync.Models;

public class GitHubAuthor
{
    public string Login { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Url { get; set; } = string.Empty;
}

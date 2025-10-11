using Newtonsoft.Json;

namespace UmbracoCommunity.Web.Features.GitHubSync.Models;

public class GitHubIssue
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public GitHubAuthor? Author { get; set; }
    public GitHubRepository Repository { get; set; } = new();
    public List<string> Labels { get; set; } = new();
}

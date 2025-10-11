namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubSyncOptions
{
    public const string SectionName = "GitHubSync";

    public string Token { get; set; } = string.Empty;
    public string Organization { get; set; } = "umbraco";
    public int RecentDays { get; set; } = 7;
    public List<string> Repositories { get; set; } = new();
    public List<string> HqOnlyTeams { get; set; } = new();
}

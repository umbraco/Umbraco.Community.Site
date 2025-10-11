namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubSyncOptions
{
    public const string SectionName = "GitHubSync";

    public string Token { get; set; } = string.Empty;
    public string Organization { get; set; } = "umbraco";
    public int RecentDays { get; set; } = 7;
    public List<RepositoryConfig> Repositories { get; set; } = new();
    public List<string> HqOnlyTeams { get; set; } = new();
}

public class RepositoryConfig
{
    public string Name { get; set; } = string.Empty;
    public string? NuGetPackageId { get; set; }

    /// <summary>
    /// Returns true if this repository has a NuGet package configured
    /// </summary>
    public bool HasNuGetPackage => !string.IsNullOrWhiteSpace(NuGetPackageId);
}

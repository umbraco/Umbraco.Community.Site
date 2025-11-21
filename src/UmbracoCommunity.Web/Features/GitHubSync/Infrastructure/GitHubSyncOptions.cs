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
    public List<string>? NuGetPackageIds { get; set; }
    public string? AnnouncementsPrefix { get; set; }

    /// <summary>
    /// Returns true if this repository has a NuGet package configured
    /// </summary>
    public bool HasNuGetPackage => !string.IsNullOrWhiteSpace(NuGetPackageId) || (NuGetPackageIds?.Any() == true);

    /// <summary>
    /// Returns all configured NuGet package IDs (handles both single and multiple package IDs)
    /// </summary>
    public IEnumerable<string> GetNuGetPackageIds()
    {
        if (NuGetPackageIds?.Any() == true)
        {
            return NuGetPackageIds;
        }
        if (!string.IsNullOrWhiteSpace(NuGetPackageId))
        {
            return new[] { NuGetPackageId };
        }
        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// Returns true if this repository has announcements in the Announcements repo with a specific prefix
    /// </summary>
    public bool HasAnnouncementsPrefix => !string.IsNullOrWhiteSpace(AnnouncementsPrefix);
}

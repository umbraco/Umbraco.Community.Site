namespace UmbracoCommunity.Web.Features.GitHubSync.Models;

public class NuGetPackageVersion
{
    public int Id { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public DateTime LastSyncedAt { get; set; }
}

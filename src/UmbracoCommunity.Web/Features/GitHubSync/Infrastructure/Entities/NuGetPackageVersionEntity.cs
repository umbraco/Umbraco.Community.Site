namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure.Entities;

public class NuGetPackageVersionEntity
{
    public int Id { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public DateTime LastSyncedAt { get; set; }
}

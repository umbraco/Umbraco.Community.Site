namespace UmbracoCommunity.Web.Features.GitHubSync.Models;

public class GitHubSyncResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Total => Added + Updated;
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public bool Success => string.IsNullOrEmpty(ErrorMessage);
}

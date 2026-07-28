namespace UmbracoCommunity.Extensions.Features.Sessionize.Models;

public class SessionizeCacheRefreshResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RefreshedAt { get; set; }
}

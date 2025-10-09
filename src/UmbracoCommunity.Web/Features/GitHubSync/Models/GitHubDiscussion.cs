using LiteDB;

namespace UmbracoCommunity.Web.Features.GitHubSync.Models;

public class GitHubDiscussion
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;
    public string Title { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public GitHubRepository Repository { get; set; } = new();
    public List<string> Labels { get; set; } = new();
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
}

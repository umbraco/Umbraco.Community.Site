namespace UmbracoCommunity.Web.Models.Api;

public class BlogPostDto
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Teaser { get; set; }
    public DateTime PublishDate { get; set; }
    public int ReadTime { get; set; }
    public string? ImageUrl { get; set; }
}

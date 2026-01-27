namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class BlogPostCardViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Teaser { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime PublishDate { get; set; }

    public int ReadTime { get; set; }
}

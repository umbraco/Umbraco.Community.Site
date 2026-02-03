namespace UmbracoCommunity.Extensions.Models;

public class CreateBlogArticleResponse
{
    public Guid ArticleKey { get; set; }
    public string ArticleName { get; set; } = string.Empty;
}

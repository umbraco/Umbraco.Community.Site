namespace UmbracoCommunity.Web.Models.Api;

public class BlogPostsResponse
{
    public List<BlogPostDto> Posts { get; set; } = [];
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
    public string? ActiveTag { get; set; }
    public string? ActiveCategory { get; set; }
    public List<BlogCategoryDto> Categories { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public class BlogCategoryDto
{
    public string Name { get; set; } = string.Empty;
}

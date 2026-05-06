using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.Api;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Controllers.Api;

[ApiController]
[Route("api/blog")]
public class BlogApiController : ControllerBase
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly ITagService _tagService;

    public BlogApiController(IUmbracoContextFactory umbracoContextFactory, ITagService tagService)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _tagService = tagService;
    }

    [HttpGet("posts/{blogKey:guid}")]
    [ProducesResponseType(typeof(BlogPostsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [OutputCache(PolicyName = OutputCachePolicies.ContentDriven)]
    public IActionResult GetPosts(
        Guid blogKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? tag = null,
        [FromQuery] string? category = null)
    {
        using var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();
        var contentCache = umbracoContextReference.UmbracoContext.Content;

        if (contentCache == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Content cache not available" });
        }

        var blogPage = contentCache.GetById(blogKey) as Blog;
        if (blogPage == null)
        {
            return NotFound(new { error = "Blog page not found" });
        }

        // Get all articles sorted by date (most recent first)
        var allArticles = blogPage.Descendants<Article>()
            .OrderByDescending(a => a.PublishDate != default ? a.PublishDate : a.CreateDate)
            .ToList();

        // If there's an explicitly set featured post, move it to the front
        var featuredArticle = blogPage.FeaturedBlogPost as Article;
        if (featuredArticle != null)
        {
            allArticles.Remove(featuredArticle);
            allArticles.Insert(0, featuredArticle);
        }

        // Start with all articles for filtering
        var articlesQuery = allArticles.AsEnumerable();

        // Filter by tag if provided
        if (!string.IsNullOrWhiteSpace(tag))
        {
            articlesQuery = articlesQuery.Where(a =>
                a.Tags != null && a.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));
        }

        // Filter by category if provided
        string? matchedCategoryName = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            articlesQuery = articlesQuery.Where(a =>
                a.Categories != null && a.Categories.Any(c =>
                    c.Name != null && c.Name.Equals(category, StringComparison.OrdinalIgnoreCase)));

            matchedCategoryName = allArticles
                .SelectMany(a => a.Categories ?? [])
                .FirstOrDefault(c => c.Name != null && c.Name.Equals(category, StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }

        var articles = articlesQuery.ToList();
        var totalItems = articles.Count;
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        var pagedArticles = articles
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDto)
            .ToList();

        // Get all categories from articles
        var categories = allArticles
            .SelectMany(a => a.Categories ?? [])
            .DistinctBy(c => c.Key)
            .OrderBy(c => c.Name)
            .Select(c => new BlogCategoryDto { Name = c.Name ?? string.Empty })
            .ToList();

        // Get all tags
        var tags = _tagService.GetAllTags(nameof(Article))
            .Select(t => t.Text)
            .OrderBy(t => t)
            .ToList();

        return Ok(new BlogPostsResponse
        {
            Posts = pagedArticles,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasPrevious = page > 1,
            HasNext = page < totalPages,
            ActiveTag = tag,
            ActiveCategory = matchedCategoryName ?? category,
            Categories = categories,
            Tags = tags
        });
    }

    private static BlogPostDto MapToDto(Article article) => new()
    {
        Title = article.Name ?? string.Empty,
        Url = article.Url() ?? string.Empty,
        Teaser = article.Teaser?.ToHtmlString(),
        PublishDate = article.PublishDate != default ? article.PublishDate : article.CreateDate,
        ReadTime = article.ReadTime,
        ImageUrl = article.ThumbnailImage?.GetCropUrl("card")
    };
}

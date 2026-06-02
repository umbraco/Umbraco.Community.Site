using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.Extensions.Models;

namespace UmbracoCommunity.Extensions.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
public class BlogArticleApiController : UmbracoCommunityExtensionsApiControllerBase
{
    private const string BlogDocTypeAlias = "blog";
    private const string BlogYearFolderDocTypeAlias = "blogYearFolder";
    private const string BlogMonthFolderDocTypeAlias = "blogMonthFolder";
    private const string ArticleDocTypeAlias = "article";

    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;

    public BlogArticleApiController(
        IContentService contentService,
        IContentTypeService contentTypeService)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
    }

    [HttpGet("blog/{nodeKey:guid}/is-blog")]
    [ProducesResponseType<bool>(StatusCodes.Status200OK)]
    public IActionResult IsBlogNode(Guid nodeKey)
    {
        var node = _contentService.GetById(nodeKey);
        var isBlog = node?.ContentType.Alias == BlogDocTypeAlias;
        return Ok(isBlog);
    }

    [HttpPost("blog/{blogNodeKey:guid}/create-article")]
    [ProducesResponseType<CreateBlogArticleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CreateBlogArticle(Guid blogNodeKey)
    {
        // Get the blog node
        var blogNode = _contentService.GetById(blogNodeKey);
        if (blogNode == null)
        {
            return NotFound("Blog node not found");
        }

        // Verify it's a blog document type
        if (blogNode.ContentType.Alias != BlogDocTypeAlias)
        {
            return BadRequest($"The specified node is not a blog. Expected '{BlogDocTypeAlias}', got '{blogNode.ContentType.Alias}'");
        }

        // Get current date info
        var now = DateTime.Now;
        var year = now.Year.ToString();
        var month = now.Month.ToString("D2"); // Two-digit format: "01" through "12"

        // Find or create year folder
        var yearFolder = FindOrCreateFolder(blogNode.Id, BlogYearFolderDocTypeAlias, year);
        if (yearFolder == null)
        {
            return BadRequest("Failed to create or find year folder");
        }

        // Find or create month folder
        var monthFolder = FindOrCreateFolder(yearFolder.Id, BlogMonthFolderDocTypeAlias, month);
        if (monthFolder == null)
        {
            return BadRequest("Failed to create or find month folder");
        }

        // Create the article
        var articleName = GenerateUniqueArticleName(monthFolder.Id);
        var articleContentType = _contentTypeService.Get(ArticleDocTypeAlias);
        if (articleContentType == null)
        {
            return BadRequest($"Content type '{ArticleDocTypeAlias}' not found");
        }

        var article = _contentService.Create(articleName, monthFolder.Id, ArticleDocTypeAlias);
        var saveResult = _contentService.Save(article);

        if (!saveResult.Success)
        {
            var errorMessages = saveResult.EventMessages?.GetAll().Select(m => m.Message) ?? [];
            return BadRequest($"Failed to create article: {string.Join(", ", errorMessages)}");
        }

        return StatusCode(StatusCodes.Status201Created, new CreateBlogArticleResponse
        {
            ArticleKey = article.Key,
            ArticleName = article.Name ?? articleName
        });
    }

    private IContent? FindOrCreateFolder(int parentId, string contentTypeAlias, string folderName)
    {
        // Try to find existing folder
        var children = _contentService.GetPagedChildren(parentId, 0, int.MaxValue, out _, [contentTypeAlias], null, null, false);
        var existingFolder = children.FirstOrDefault(c => c.Name == folderName);

        if (existingFolder != null)
        {
            return existingFolder;
        }

        // Create new folder
        var contentType = _contentTypeService.Get(contentTypeAlias);
        if (contentType == null)
        {
            return null;
        }

        var folder = _contentService.Create(folderName, parentId, contentTypeAlias);
        var saveResult = _contentService.Save(folder);

        return saveResult.Success ? folder : null;
    }

    private string GenerateUniqueArticleName(int parentId)
    {
        const string baseName = "New Article";

        var children = _contentService.GetPagedChildren(parentId, 0, int.MaxValue, out _, [ArticleDocTypeAlias], null, null, false);
        var existingNames = children
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        // Add timestamp for uniqueness
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return $"{baseName} {timestamp}";
    }
}

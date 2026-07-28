using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Services.Documentation.Search;

namespace UmbracoCommunity.Web.Controllers.Api;

[ApiController]
[Route("api/documentation")]
public class DocumentationSearchApiController : ControllerBase
{
    private const int DefaultMaxResults = 8;
    private const int MaxAllowedResults = 20;

    private readonly IDocumentationSearchService _searchService;

    public DocumentationSearchApiController(IDocumentationSearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentationSearchHit>), StatusCodes.Status200OK)]
    public IActionResult Search([FromQuery] string? q, [FromQuery] int? max = null)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<DocumentationSearchHit>());
        }

        var take = Math.Clamp(max ?? DefaultMaxResults, 1, MaxAllowedResults);
        var hits = _searchService.Search(q, take);
        return Ok(hits);
    }
}

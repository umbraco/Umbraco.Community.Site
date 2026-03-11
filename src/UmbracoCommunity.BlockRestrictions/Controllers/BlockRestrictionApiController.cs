using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.BlockRestrictions.Models;

namespace UmbracoCommunity.BlockRestrictions.Controllers;

/// <summary>
/// API controller for block restriction operations.
/// Provides endpoints for:
///   - Resolving effective restrictions for a content node (used by property editors)
///   - CRUD operations on restriction rules (used by the workspace view)
///   - Querying element types and data types (used by the workspace view)
///
/// All endpoints require backoffice authentication (inherited from base controller).
/// Read-only endpoints include response cache headers for browser-level caching.
/// </summary>
public class BlockRestrictionApiController : BlockRestrictionApiControllerBase
{
    private readonly BlockRestrictionService _service;

    public BlockRestrictionApiController(BlockRestrictionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Resolves the effective block restrictions for a content node by walking
    /// up the content tree. This is the primary endpoint called by the restricted
    /// property editors when they load in the content editor.
    ///
    /// For existing content: resolves by walking up from the node in the content tree.
    /// For new content (node doesn't exist yet): falls back to checking the document
    /// type directly, then walking up from the parent node. The contentTypeKey and
    /// parentKey query parameters enable this fallback.
    ///
    /// Returns 404 only if resolution fails entirely (no node, no fallback params).
    /// Returns HasRestrictions=false if no rules are found at any level (fail-open).
    ///
    /// Cached for 60s in the browser (same node won't re-fetch during an editing session).
    /// Also cached server-side for 60s in the service layer.
    /// </summary>
    [HttpGet("allowed-blocks/{nodeKey:guid}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetAllowedBlocks(
        Guid nodeKey,
        [FromQuery] Guid? contentTypeKey = null,
        [FromQuery] Guid? parentKey = null)
    {
        // Try the standard resolution path (existing content node).
        var result = await _service.ResolveAllowedBlocksForNodeAsync(nodeKey);

        // If the node wasn't found (new content), fall back to content type + parent.
        if (result == null && (contentTypeKey.HasValue || parentKey.HasValue))
        {
            result = await _service.ResolveForNewContentAsync(contentTypeKey, parentKey);
        }

        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// Gets the restriction rule for a specific document type (without tree walking).
    /// Used by the workspace view to load the current config when opening the Blocks tab.
    /// Returns null/empty body if no rule exists (the doc type inherits from ancestors).
    /// </summary>
    [HttpGet("rules/{docTypeKey:guid}")]
    public async Task<IActionResult> GetRule(Guid docTypeKey)
    {
        var result = await _service.GetConfigForDocumentTypeAsync(docTypeKey);
        return Ok(result);
    }

    /// <summary>
    /// Creates or updates a restriction rule for a document type.
    /// Called when the user clicks "Update" in the workspace view with restrictions enabled.
    /// Invalidates the server-side resolved cache (via the service's version counter).
    /// </summary>
    [HttpPut("rules/{docTypeKey:guid}")]
    public async Task<IActionResult> SaveRule(Guid docTypeKey, [FromBody] SaveBlockRestrictionRequest request)
    {
        await _service.SaveConfigAsync(docTypeKey, request.AllowedBlockAliases);
        return Ok();
    }

    /// <summary>
    /// Deletes the restriction rule for a document type, returning it to inheritance.
    /// Called when the user toggles off restrictions and clicks "Update" in the workspace view.
    /// Returns 404 if no rule existed to delete.
    /// </summary>
    [HttpDelete("rules/{docTypeKey:guid}")]
    public async Task<IActionResult> DeleteRule(Guid docTypeKey)
    {
        var deleted = await _service.DeleteConfigAsync(docTypeKey);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    /// <summary>
    /// Lists all element types in Umbraco (content types where IsElement = true).
    /// Used by the workspace view to populate the block type checklist.
    /// Cached for 5 minutes in the browser — element types change infrequently.
    /// </summary>
    [HttpGet("element-types")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public IActionResult GetElementTypes()
    {
        var result = _service.GetAllElementTypes();
        return Ok(result);
    }

    /// <summary>
    /// Lists all data types using the restricted Block Grid or Block List property
    /// editor UIs, along with their configured block element type keys.
    /// Used by the workspace view's "Filter by data type" dropdown.
    /// Cached for 5 minutes in the browser — data type configuration changes infrequently.
    /// </summary>
    [HttpGet("block-data-types")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetBlockDataTypes()
    {
        var result = await _service.GetBlockDataTypes();
        return Ok(result);
    }
}

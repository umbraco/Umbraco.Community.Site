using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.BlockRestrictions.Models;

namespace UmbracoCommunity.BlockRestrictions.Controllers;

public class BlockRestrictionApiController : BlockRestrictionApiControllerBase
{
    private readonly BlockRestrictionService _service;

    public BlockRestrictionApiController(BlockRestrictionService service)
    {
        _service = service;
    }

    [HttpGet("allowed-blocks/{nodeKey:guid}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetAllowedBlocks(Guid nodeKey)
    {
        var result = await _service.ResolveAllowedBlocksForNodeAsync(nodeKey);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpGet("rules/{docTypeKey:guid}")]
    public async Task<IActionResult> GetRule(Guid docTypeKey)
    {
        var result = await _service.GetConfigForDocumentTypeAsync(docTypeKey);
        return Ok(result);
    }

    [HttpPut("rules/{docTypeKey:guid}")]
    public async Task<IActionResult> SaveRule(Guid docTypeKey, [FromBody] SaveBlockRestrictionRequest request)
    {
        await _service.SaveConfigAsync(docTypeKey, request.AllowedBlockAliases);
        return Ok();
    }

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

    [HttpGet("element-types")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public IActionResult GetElementTypes()
    {
        var result = _service.GetAllElementTypes();
        return Ok(result);
    }

    [HttpGet("block-data-types")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetBlockDataTypes()
    {
        var result = await _service.GetBlockDataTypes();
        return Ok(result);
    }
}

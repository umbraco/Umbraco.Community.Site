using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoCommunity.Extensions.Infrastructure;
using UmbracoCommunity.Web.Features.Members.Migration;

namespace UmbracoCommunity.Extensions.Controllers;

/// <summary>
/// Backoffice endpoints for converting content-based community members to lightweight external
/// members. <c>GET</c> is a dry run; <c>POST</c> changes data and requires <c>confirm=true</c>.
/// Restricted to backoffice users with access to the Members section.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMembers)]
public sealed class ExternalMemberMigrationApiController : UmbracoCommunityExtensionsApiControllerBase
{
    private readonly ExternalMemberMigrationService _service;

    public ExternalMemberMigrationApiController(ExternalMemberMigrationService service)
    {
        _service = service;
    }

    [HttpGet("members/external-migration/plan")]
    [ProducesResponseType<ExternalMemberMigrationReport>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalMemberMigrationReport>> Plan(CancellationToken cancellationToken) =>
        await _service.PlanAsync(cancellationToken);

    [HttpPost("members/external-migration/run")]
    [ProducesResponseType<ExternalMemberMigrationReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExternalMemberMigrationReport>> Run(
        [FromQuery] bool confirm = false,
        [FromQuery] int? limit = null)
    {
        if (!confirm)
        {
            return BadRequest("This deletes content-based members and recreates them as external members. Run the plan first, then pass confirm=true.");
        }

        if (limit is <= 0)
        {
            return BadRequest("limit must be a positive number.");
        }

        try
        {
            // Not linked to the request: stopping half way through a member would be worse than finishing the batch.
            return await _service.MigrateAsync(limit, CancellationToken.None);
        }
        catch (ExternalMemberMigrationAlreadyRunningException ex)
        {
            return Conflict(ex.Message);
        }
    }
}

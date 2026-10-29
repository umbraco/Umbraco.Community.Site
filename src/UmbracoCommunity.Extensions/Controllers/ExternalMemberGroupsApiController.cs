using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoCommunity.Extensions.Infrastructure;
using UmbracoCommunity.Extensions.Models;
using UmbracoCommunity.Web.Features.Members.Groups;

namespace UmbracoCommunity.Extensions.Controllers;

/// <summary>
/// Backoffice endpoints for viewing and changing the member groups of lightweight external-only
/// members, which Umbraco 18.3's own member editor shows read-only. Used by the "Member groups"
/// workspace view on external members. Restricted to backoffice users with access to the Members section.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.Extensions")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMembers)]
public sealed class ExternalMemberGroupsApiController : UmbracoCommunityExtensionsApiControllerBase
{
    private readonly ExternalMemberGroupService _service;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

    public ExternalMemberGroupsApiController(
        ExternalMemberGroupService service,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        _service = service;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    [HttpGet("members/{memberKey:guid}/external-groups")]
    [ProducesResponseType<ExternalMemberGroups>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExternalMemberGroups>> GetExternalMemberGroups(Guid memberKey)
    {
        var groups = await _service.GetAsync(memberKey);
        return groups == null ? ExternalMemberNotFound() : groups;
    }

    [HttpPut("members/{memberKey:guid}/external-groups")]
    [ProducesResponseType<ExternalMemberGroups>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExternalMemberGroups>> SetExternalMemberGroups(
        Guid memberKey,
        UpdateExternalMemberGroupsRequest request)
    {
        var user = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        var changedBy = user == null ? "An unknown backoffice user" : $"Backoffice user {user.Name} ({user.Key})";

        var result = await _service.SetAsync(memberKey, request.GroupKeys, changedBy);

        return result.Status switch
        {
            ExternalMemberGroupsUpdateStatus.Success => result.Groups!,
            ExternalMemberGroupsUpdateStatus.MemberNotFound => ExternalMemberNotFound(),
            ExternalMemberGroupsUpdateStatus.UnknownGroups => BadRequest(new ProblemDetails
            {
                Title = "Unknown member group",
                Detail = $"These member groups don't exist: {string.Join(", ", result.UnknownGroupKeys)}. Nothing was changed.",
                Status = StatusCodes.Status400BadRequest,
            }),
            _ => Problem(
                title: "Could not change the member groups",
                detail: "Nothing was changed. See the log for details.",
                statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private NotFoundObjectResult ExternalMemberNotFound() =>
        NotFound(new ProblemDetails
        {
            Title = "External member not found",
            Detail = "There is no external-only member with this key. Content-based members are edited in the normal member editor.",
            Status = StatusCodes.Status404NotFound,
        });
}

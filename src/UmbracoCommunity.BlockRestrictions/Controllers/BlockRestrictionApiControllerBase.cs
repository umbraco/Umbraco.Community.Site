using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace UmbracoCommunity.BlockRestrictions.Controllers;

/// <summary>
/// Base controller for all Block Restrictions API endpoints.
/// Configures routing, authentication, API versioning, and Swagger grouping
/// so that concrete controllers only need to define their action methods.
///
/// Key attributes:
///   - [BackOfficeRoute] — sets the URL prefix for all endpoints
///   - [Authorize] — requires the user to have Content section access in the backoffice
///   - [MapToApi] — associates endpoints with our Swagger document for API docs
///   - [ApiVersion] — enables API versioning (currently v1)
/// </summary>
[ApiController]
[BackOfficeRoute("umbracocommunityblockrestrictions/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(Constants.ApiName)]
[ApiVersion("1.0")]
public abstract class BlockRestrictionApiControllerBase : ControllerBase
{
}

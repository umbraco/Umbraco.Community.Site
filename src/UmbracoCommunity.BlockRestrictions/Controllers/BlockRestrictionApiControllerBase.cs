using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace UmbracoCommunity.BlockRestrictions.Controllers;

[ApiController]
[BackOfficeRoute("umbracocommunityblockrestrictions/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(Constants.ApiName)]
[ApiVersion("1.0")]
public abstract class BlockRestrictionApiControllerBase : ControllerBase
{
}

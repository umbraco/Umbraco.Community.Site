using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace UmbracoCommunity.SearchTracker.Api;

public static class SearchTrackerApiConstants
{
    public const string ApiName = "searchtracker";
}

[ApiController]
[BackOfficeRoute("searchtracker/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(SearchTrackerApiConstants.ApiName)]
[ApiVersion("1.0")]
public abstract class SearchTrackerApiControllerBase : ControllerBase
{
}

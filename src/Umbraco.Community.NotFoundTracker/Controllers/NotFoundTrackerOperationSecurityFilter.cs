using Umbraco.Cms.Api.Management.OpenApi;

namespace Umbraco.Community.NotFoundTracker.Controllers;

internal sealed class NotFoundTrackerOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
{
    protected override string ApiName => Constants.ApiName;
}

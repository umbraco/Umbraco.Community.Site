using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace UmbracoCommunity.ProfileMatchReview.Api;

/// <summary>
/// Base for the profile match review management API. Backoffice bearer auth, then a check that the user is in the
/// reviewer group. Routed under <c>/umbraco/profilematchreview/api/v{version}</c>.
/// </summary>
[ApiController]
[BackOfficeRoute("profilematchreview/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMembers)]
[MapToApi(ProfileMatchReviewApiConstants.ApiName)]
[ApiVersion("1.0")]
public abstract class ProfileMatchReviewApiControllerBase : ControllerBase
{
    protected ProfileMatchReviewApiControllerBase(IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    {
        BackOfficeSecurityAccessor = backOfficeSecurityAccessor;
    }

    protected IBackOfficeSecurityAccessor BackOfficeSecurityAccessor { get; }

    protected IUser? CurrentUser => BackOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;

    /// <summary>True when the signed-in backoffice user is in the reviewer group or is an administrator.</summary>
    protected bool IsReviewer =>
        CurrentUser?.Groups.Any(g =>
            g.Alias == ProfileMatchReviewApiConstants.ReviewerGroupAlias || g.Alias == Constants.Security.AdminGroupAlias) == true;

    protected ObjectResult NotReviewer() =>
        Problem(
            title: "No access",
            detail: "Profile match review is limited to the Profile match review user group.",
            statusCode: StatusCodes.Status403Forbidden);
}

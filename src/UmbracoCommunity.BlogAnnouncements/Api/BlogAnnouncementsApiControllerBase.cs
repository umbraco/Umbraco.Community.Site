using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace UmbracoCommunity.BlogAnnouncements.Api;

/// <summary>
/// Base for the Blog Announcements management API. Backoffice bearer auth, Content-section access —
/// the same policy as the BlockRestrictions and NotFoundTracker management APIs. Routed under
/// <c>/umbraco/blogannouncements/api/v{version}</c>.
/// </summary>
[ApiController]
[BackOfficeRoute("blogannouncements/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(BlogAnnouncementsApiConstants.ApiName)]
[ApiVersion("1.0")]
public abstract class BlogAnnouncementsApiControllerBase : ControllerBase
{
}

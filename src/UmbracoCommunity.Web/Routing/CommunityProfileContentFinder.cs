using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Routing;

/// <summary>
/// Intercepts requests under the tenant's single Community Profile page and resolves the
/// trailing path segment as a member's GitHub handle, stashing it for the render controller.
/// Mirrors <see cref="DocumentationContentFinder"/>'s tenant-scoping and remainder-segment
/// technique, simplified since <c>CommunityProfilePage</c> is a singleton (one node, not many).
/// </summary>
/// <remarks>
/// Only matches when the handle segment looks like a plausible GitHub username — anything
/// else (garbage paths, multiple segments) falls through to the normal 404 flow instead of
/// showing a "claim this profile" CTA for input that could never be a real handle.
/// </remarks>
public class CommunityProfileContentFinder : IContentFinder
{
    public const string HandleItemKey = "CommunityProfile:Handle";

    // GitHub username rules: alphanumeric and single hyphens, no leading/trailing/doubled
    // hyphen, max 39 characters.
    private static readonly Regex HandlePattern = new(
        @"^[a-zA-Z\d](?:[a-zA-Z\d]|-(?=[a-zA-Z\d])){0,38}$",
        RegexOptions.Compiled);

    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CommunityProfileContentFinder(
        IUmbracoContextAccessor umbracoContextAccessor,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory serviceScopeFactory)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _httpContextAccessor = httpContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
        {
            return Task.FromResult(false);
        }

        var rootContentId = request.Domain?.ContentId;
        CommunityProfilePage? profilePage;

        if (rootContentId is not null
            && umbracoContext.Content?.GetById(rootContentId.Value) is { } tenantRoot)
        {
            profilePage = tenantRoot.DescendantsOrSelf<CommunityProfilePage>().FirstOrDefault();
        }
        else
        {
            // Local/single-tenant setups without a domain binding — walk all root nodes via
            // a scoped IPublishedContentQuery (content finders are singletons; the query is scoped).
            using var scope = _serviceScopeFactory.CreateScope();
            var contentQuery = scope.ServiceProvider.GetRequiredService<IPublishedContentQuery>();
            profilePage = contentQuery.ContentAtRoot()
                .SelectMany(r => r.DescendantsOrSelf<CommunityProfilePage>())
                .FirstOrDefault();
        }

        if (profilePage == null)
        {
            return Task.FromResult(false);
        }

        var pageUrl = profilePage.Url(mode: UrlMode.Relative);
        if (string.IsNullOrEmpty(pageUrl) || pageUrl == "#")
        {
            return Task.FromResult(false);
        }

        var normalised = pageUrl.TrimEnd('/');
        var requestPath = request.Uri.AbsolutePath;

        if (!requestPath.StartsWith(normalised + "/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        var handle = requestPath[(normalised.Length + 1)..].Trim('/');
        if (string.IsNullOrEmpty(handle) || handle.Contains('/') || !HandlePattern.IsMatch(handle))
        {
            return Task.FromResult(false);
        }

        request.SetPublishedContent(profilePage);

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            httpContext.Items[HandleItemKey] = handle;
        }

        return Task.FromResult(true);
    }
}

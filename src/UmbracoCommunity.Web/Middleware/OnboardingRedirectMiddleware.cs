using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Middleware;

/// <summary>
/// Onboards a signed-in member automatically the first time they're seen without a completed
/// profile, and sends them straight to their account page. Fires on every request after Umbraco's
/// own auth pipeline has run, regardless of how the member arrived (GitHub callback, a direct
/// profile visit, a shared link) — the same technique as <see cref="BlogFolderRedirectMiddleware"/>.
/// </summary>
/// <remarks>
/// The setup itself (profile row, GitHub name/company/bio as the member's own text, onboarding
/// completed, profile left hidden) is <see cref="ProfileAutoOnboardingService"/>. Once completed,
/// a member costs one profile lookup per page request and is never redirected again. Only page
/// requests are handled, since the account page is resolved from the current content's tenant
/// (<see cref="PublishedContentExtensions.GetSingletonPage{T}"/>); a request already for the account
/// page, or a tenant without one, continues without a redirect. Failures never block the request.
/// </remarks>
public class OnboardingRedirectMiddleware
{
    private static readonly string[] ExcludedPathPrefixes =
    [
        "/umbraco",
        "/media",
        "/api/",
        "/logout",
    ];

    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly ILogger<OnboardingRedirectMiddleware> _logger;
    private readonly RequestDelegate _next;

    public OnboardingRedirectMiddleware(
        IUmbracoContextAccessor umbracoContextAccessor,
        ILogger<OnboardingRedirectMiddleware> logger,
        RequestDelegate next)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _logger = logger;
        _next = next;
    }

    // IMemberManager/ProfileAutoOnboardingService are scoped services — they must be resolved per
    // request via InvokeAsync parameters (ASP.NET Core's DI convention for middleware),
    // not the constructor, which only ever runs once against the app's root provider.
    public async Task InvokeAsync(HttpContext context, IMemberManager memberManager, ProfileAutoOnboardingService autoOnboarding)
    {
        if (!ShouldCheck(context) || !memberManager.IsLoggedIn())
        {
            await _next(context);
            return;
        }

        // The account page is found relative to the requested page's tenant, so only page
        // requests can be onboarded (and redirected); anything else waits for the next page.
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext)
            || umbracoContext?.PublishedRequest?.PublishedContent is not { } currentContent)
        {
            await _next(context);
            return;
        }

        bool onboarded;
        try
        {
            var member = await memberManager.GetCurrentMemberAsync();
            onboarded = member != null
                && await autoOnboarding.EnsureOnboardedAsync(member.Key, member.UserName, member.Name, context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Automatic onboarding failed; continuing without it");
            onboarded = false;
        }

        if (!onboarded
            || string.Equals(currentContent.ContentType.Alias, AccountPage.ModelTypeAlias, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var accountPage = currentContent.GetSingletonPage<AccountPage>();
        if (accountPage == null)
        {
            await _next(context);
            return;
        }

        context.Response.Redirect(accountPage.Url(), permanent: false);
    }

    private static bool ShouldCheck(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return !ExcludedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

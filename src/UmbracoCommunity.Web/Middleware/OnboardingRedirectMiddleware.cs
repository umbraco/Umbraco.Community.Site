using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Middleware;

/// <summary>
/// Redirects a signed-in member who has never started onboarding to the Onboarding page,
/// regardless of how they arrived (GitHub callback, a direct profile visit, a manually
/// shared link). Fires on every request after Umbraco's own auth pipeline has run — the
/// same technique as <see cref="BlogFolderRedirectMiddleware"/>.
/// </summary>
/// <remarks>
/// Only <see cref="OnboardingStatus.NotStarted"/> (or no profile row at all) triggers the
/// redirect. Once a member has started (<see cref="OnboardingStatus.InProgress"/>), they're
/// left alone to navigate anywhere — e.g. the "email the DevRel team" link, or any other
/// site link — rather than being funnelled back to onboarding on every single request,
/// which would trap them with no way to leave the page.
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
    private readonly RequestDelegate _next;

    public OnboardingRedirectMiddleware(
        IUmbracoContextAccessor umbracoContextAccessor,
        RequestDelegate next)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _next = next;
    }

    // IMemberManager/MemberProfileStore are scoped services — they must be resolved per
    // request via InvokeAsync parameters (ASP.NET Core's DI convention for middleware),
    // not the constructor, which only ever runs once against the app's root provider.
    public async Task InvokeAsync(HttpContext context, IMemberManager memberManager, MemberProfileStore store)
    {
        if (!ShouldCheck(context) || !memberManager.IsLoggedIn())
        {
            await _next(context);
            return;
        }

        var member = await memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            await _next(context);
            return;
        }

        var profile = await store.GetByMemberKeyAsync(member.Key, context.RequestAborted);
        if (profile != null && profile.OnboardingStatus != OnboardingStatus.NotStarted)
        {
            // Already started (in progress) or completed — let them navigate freely.
            await _next(context);
            return;
        }

        if (_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext)
            && umbracoContext?.PublishedRequest?.PublishedContent is { } currentContent)
        {
            // Already on the onboarding page — don't redirect to itself.
            if (string.Equals(currentContent.ContentType.Alias, OnboardingPage.ModelTypeAlias, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var onboardingPage = currentContent.GetSingletonPage<OnboardingPage>();

            if (onboardingPage != null)
            {
                var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
                context.Response.Redirect($"{onboardingPage.Url()}?returnUrl={returnUrl}", permanent: false);
                return;
            }
        }

        await _next(context);
    }

    private static bool ShouldCheck(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return !ExcludedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

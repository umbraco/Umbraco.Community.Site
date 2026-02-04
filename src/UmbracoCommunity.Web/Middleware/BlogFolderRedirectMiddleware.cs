using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Middleware;

public class BlogFolderRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;

    private static readonly string[] FolderAliases =
    [
        BlogYearFolder.ModelTypeAlias,
        BlogMonthFolder.ModelTypeAlias
    ];

    public BlogFolderRedirectMiddleware(
        IUmbracoContextAccessor umbracoContextAccessor,
        RequestDelegate next)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) &&
            umbracoContext?.PublishedRequest?.PublishedContent is { } content)
        {
            var contentTypeAlias = content.ContentType.Alias;

            if (FolderAliases.Contains(contentTypeAlias, StringComparer.OrdinalIgnoreCase))
            {
                var blogPage = content.AncestorOrSelf<Blog>();
                var blogUrl = blogPage?.Url() ?? "/";

                context.Response.Redirect(blogUrl, permanent: true);
                return;
            }
        }

        await _next(context);
    }
}

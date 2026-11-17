using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// On a member profile API: after a successful change (any method but GET or HEAD, a 2xx answer) made by a profile admin
/// on someone else's profile, writes one audit line with both member keys and the action's name (see
/// <see cref="ProfileEditSubjectResolver.LogEditAs"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ProfileEditAuditAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || executed.Exception != null)
        {
            return;
        }

        var resolver = context.HttpContext.RequestServices.GetService<ProfileEditSubjectResolver>();
        var status = executed.Result switch
        {
            IStatusCodeActionResult { StatusCode: { } code } => code,
            EmptyResult => StatusCodes.Status200OK,
            _ => context.HttpContext.Response.StatusCode,
        };

        if (resolver?.Resolved is { IsEditAs: true } subject && status is >= 200 and < 300)
        {
            resolver.LogEditAs(subject, context.ActionDescriptor is ControllerActionDescriptor action ? $"{action.ControllerName}.{action.ActionName}" : context.ActionDescriptor.Id);
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Middleware;

/// <summary>
/// Middleware to validate Umbraco Forms submissions and prevent bot attacks with invalid form IDs
/// </summary>
public class FormValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FormValidationMiddleware> _logger;

    private static readonly Guid EmptyGuid = Guid.Empty;

    public FormValidationMiddleware(RequestDelegate next, ILogger<FormValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check POST requests to Umbraco Forms endpoints
        if (context.Request.Method == "POST" &&
            context.Request.Path.StartsWithSegments("/umbraco/forms", StringComparison.OrdinalIgnoreCase))
        {
            // Check if form data contains formId parameter
            if (context.Request.HasFormContentType && context.Request.Form != null)
            {
                var formId = context.Request.Form["formId"].ToString();

                if (!string.IsNullOrEmpty(formId))
                {
                    // Validate the GUID format
                    if (!Guid.TryParse(formId, out Guid parsedFormId))
                    {
                        _logger.LogWarning("Bot attempt: Invalid form ID format '{FormId}' from IP {IpAddress}",
                            formId, context.Connection.RemoteIpAddress);
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid form ID format");
                        return;
                    }

                    // Check for empty GUID (common bot pattern)
                    if (parsedFormId == EmptyGuid)
                    {
                        _logger.LogWarning("Bot attempt: Empty GUID form submission from IP {IpAddress}",
                            context.Connection.RemoteIpAddress);
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid form ID");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}

using Microsoft.AspNetCore.Http;

namespace UmbracoCommunity.Web.Abstract.Services;

/// <summary>Caps paid AI suggestion lookups per visitor and site-wide; bots get none.</summary>
public interface ISuggestionBudget
{
    bool TryConsume(HttpContext? httpContext);
}

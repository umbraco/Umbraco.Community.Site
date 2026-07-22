using System.Net;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Thrown when the content platform responds with a non-success status. Carries the parsed
/// <c>error.message</c>/<c>error.code</c> from its <c>{"error":{"code":"...","message":"..."}}</c> envelope when
/// present, so callers (e.g. an invalid-feed 400) can be relayed to the end user verbatim instead of being
/// flattened into a generic failure.
/// </summary>
public sealed class CommunityBlogsApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public string? ErrorCode { get; }

    public bool IsServerError => (int)StatusCode >= 500;

    public CommunityBlogsApiException(HttpStatusCode statusCode, string? errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

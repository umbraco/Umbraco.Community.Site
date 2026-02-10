using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;

namespace UmbracoCommunity.Web.Utilities;

/// <summary>
/// Utility service for URL operations, particularly converting relative URLs to absolute.
/// </summary>
internal class UrlUtilities
{
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UrlUtilities(
        IPublishedUrlProvider publishedUrlProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _publishedUrlProvider = publishedUrlProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the absolute URL for published content.
    /// </summary>
    public string? GetAbsoluteUrl(IPublishedContent content)
    {
        var relativeUrl = content.Url(_publishedUrlProvider, null, UrlMode.Relative);
        return MakeAbsoluteUrl(relativeUrl);
    }

    /// <summary>
    /// Converts a relative URL to an absolute URL using the current request's scheme and host.
    /// Returns the URL unchanged if it's already absolute.
    /// Returns null if the URL is empty or there's no HTTP context.
    /// </summary>
    public string? MakeAbsoluteUrl(string? relativeUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl))
        {
            return null;
        }

        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out _))
        {
            return relativeUrl;
        }

        var baseUri = GetCurrentBaseUri();
        if (baseUri is null)
        {
            return null;
        }

        return new Uri(baseUri, relativeUrl).ToString();
    }

    /// <summary>
    /// Gets the base URI from the current HTTP request (scheme + host).
    /// Returns null if there's no HTTP context.
    /// </summary>
    public Uri? GetCurrentBaseUri()
    {
        if (_httpContextAccessor.HttpContext is null)
        {
            return null;
        }

        var request = _httpContextAccessor.HttpContext.Request;
        return new Uri($"{request.Scheme}://{request.Host}");
    }
}

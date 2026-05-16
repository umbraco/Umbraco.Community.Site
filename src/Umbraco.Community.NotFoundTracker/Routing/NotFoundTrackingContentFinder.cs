using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Recording;

namespace Umbraco.Community.NotFoundTracker.Routing;

/// <summary>
/// Last-chance content finder. Records the 404 hit (unless an ignore rule applies) and
/// delegates to the host's <see cref="INotFoundPageResolver"/> to find the content node
/// to render. The recording side does NO awaited I/O — it pushes a non-blocking event
/// to <see cref="NotFoundHitChannel"/>.
/// </summary>
public sealed class NotFoundTrackingContentFinder : IContentLastChanceFinder
{
    private readonly INotFoundIgnoreRuleMatcher _matcher;
    private readonly NotFoundHitChannel _channel;
    private readonly INotFoundPageResolver _resolver;
    private readonly ILogger<NotFoundTrackingContentFinder> _logger;

    public NotFoundTrackingContentFinder(
        INotFoundIgnoreRuleMatcher matcher,
        NotFoundHitChannel channel,
        INotFoundPageResolver resolver,
        ILogger<NotFoundTrackingContentFinder> logger)
    {
        _matcher = matcher;
        _channel = channel;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        TryRecord(request);

        var page = await _resolver.ResolveAsync(request);
        if (page is null)
        {
            return false;
        }

        request.SetPublishedContent(page);
        request.SetResponseStatus(404);
        return true;
    }

    private void TryRecord(IPublishedRequestBuilder request)
    {
        try
        {
            var hostname = UrlNormalizer.NormalizeHostname(request.Domain?.Name ?? request.Uri.Host);
            var path = UrlNormalizer.NormalizePath(request.Uri.AbsolutePath);

            if (_matcher.IsIgnored(hostname, path))
            {
                return;
            }

            var queryString = string.IsNullOrEmpty(request.Uri.Query) ? null : request.Uri.Query;

            // User-Agent isn't on IPublishedRequestBuilder directly — passed through via the
            // HttpContext accessor in a richer implementation. For now we record null; this
            // is wired up if/when we need it. Keeping the field is forward-compatible.
            var userAgent = (string?)null;

            var evt = new NotFoundHitEvent(hostname, path, queryString, userAgent, DateTime.UtcNow);
            _channel.Writer.TryWrite(evt);  // non-blocking; drops silently if channel is full.
        }
        catch (Exception ex)
        {
            // Never let recording break the request.
            _logger.LogWarning(ex, "NotFoundTracker recording failed");
        }
    }
}

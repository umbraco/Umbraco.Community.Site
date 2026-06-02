using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Notifications;

/// <summary>
/// Handles cache invalidation for blog content when articles or blog pages are published/unpublished.
/// Uses Umbraco's ContentCacheRefresherNotification to detect content changes and evicts
/// the output cache tagged with <see cref="OutputCacheTags.BlogContent"/>.
/// </summary>
public class BlogContentCacheInvalidationHandler : INotificationAsyncHandler<ContentCacheRefresherNotification>
{
    private readonly IOutputCacheStore _outputCacheStore;
    private readonly ILogger<BlogContentCacheInvalidationHandler> _logger;

    /// <summary>
    /// Content type aliases that should trigger blog cache invalidation.
    /// </summary>
    private static readonly HashSet<string> BlogContentTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        Article.ModelTypeAlias,
        Blog.ModelTypeAlias
    };

    public BlogContentCacheInvalidationHandler(
        IOutputCacheStore outputCacheStore,
        ILogger<BlogContentCacheInvalidationHandler> logger)
    {
        _outputCacheStore = outputCacheStore;
        _logger = logger;
    }

    public async Task HandleAsync(ContentCacheRefresherNotification notification, CancellationToken cancellationToken)
    {
        // Check if any of the refreshed content items are blog-related
        var shouldInvalidate = notification.MessageObject switch
        {
            // Single content refresh
            Umbraco.Cms.Core.Sync.RefreshInstruction instruction when
                IsBlogContentType(instruction.JsonPayload) => true,

            // Multiple content refresh
            IEnumerable<Umbraco.Cms.Core.Sync.RefreshInstruction> instructions when
                instructions.Any(i => IsBlogContentType(i.JsonPayload)) => true,

            _ => false
        };

        if (shouldInvalidate)
        {
            _logger.LogInformation("Blog content changed, invalidating output cache for tag '{Tag}'", OutputCacheTags.BlogContent);
            await _outputCacheStore.EvictByTagAsync(OutputCacheTags.BlogContent, cancellationToken);
        }
    }

    private static bool IsBlogContentType(string? jsonPayload)
    {
        if (string.IsNullOrEmpty(jsonPayload))
            return false;

        // The payload contains content type alias information
        // Check if any blog-related content types are in the payload
        return BlogContentTypeAliases.Any(alias =>
            jsonPayload.Contains(alias, StringComparison.OrdinalIgnoreCase));
    }
}

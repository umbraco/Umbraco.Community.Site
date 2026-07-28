using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Services.Documentation.Search;

/// <summary>
/// Seeds the documentation Examine index on app startup. Subsequent rebuilds are driven by
/// <see cref="IDocumentationService.IndexRebuilt"/> events (raised by the file watcher).
/// </summary>
public sealed class DocumentationIndexerStartupHostedService : IHostedService
{
    private readonly IDocumentationIndexer _indexer;
    private readonly ILogger<DocumentationIndexerStartupHostedService> _logger;

    public DocumentationIndexerStartupHostedService(
        IDocumentationIndexer indexer,
        ILogger<DocumentationIndexerStartupHostedService> logger)
    {
        _indexer = indexer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _indexer.RebuildIndex();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial documentation index build failed.");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

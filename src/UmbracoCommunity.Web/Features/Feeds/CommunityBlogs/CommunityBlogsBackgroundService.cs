using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>Aggregates community blog posts on startup and then on a periodic timer.</summary>
public sealed class CommunityBlogsBackgroundService : BackgroundService
{
    private readonly ICommunityBlogsService _service;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly ILogger<CommunityBlogsBackgroundService> _logger;

    public CommunityBlogsBackgroundService(
        ICommunityBlogsService service,
        IOptionsMonitor<CommunityBlogsOptions> options,
        ILogger<CommunityBlogsBackgroundService> logger)
    {
        _service = service;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshSafelyAsync(stoppingToken);

        var interval = TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RefreshIntervalInHours));
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _service.RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while refreshing community blog posts.");
        }
    }
}

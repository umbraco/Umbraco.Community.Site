using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

/// <summary>
/// A Contentment data source that provides Sessionize rooms for use in Data List editors.
/// </summary>
public class SessionizeRoomsDataSource : IContentmentDataSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionizeRoomsDataSource> _logger;

    public SessionizeRoomsDataSource(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionizeRoomsDataSource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Name => "Sessionize Rooms";

    public string Description => "Data source for rooms from the configured Sessionize event.";

    public string Icon => "icon-company";

    public string Group => "Sessionize";

    public OverlaySize OverlaySize => OverlaySize.Small;

    public Dictionary<string, object>? DefaultValues => null;

    public IEnumerable<ContentmentConfigurationField> Fields => Enumerable.Empty<ContentmentConfigurationField>();

    public IEnumerable<DataListItem> GetItems(Dictionary<string, object> config)
    {
        try
        {
            // Create a scope to resolve the scoped SessionizeApiClient from this singleton data source.
            using var scope = _scopeFactory.CreateScope();
            var sessionizeClient = scope.ServiceProvider.GetRequiredService<SessionizeApiClient>();
            var rooms = sessionizeClient.GetRoomsAsync().GetAwaiter().GetResult();

            return rooms
                .OrderBy(r => r.Sort)
                .Select(r => new DataListItem
                {
                    Name = r.Name,
                    Value = r.Id.ToString(),
                    Icon = "icon-company"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch rooms from Sessionize for Contentment data source");
            return Enumerable.Empty<DataListItem>();
        }
    }
}

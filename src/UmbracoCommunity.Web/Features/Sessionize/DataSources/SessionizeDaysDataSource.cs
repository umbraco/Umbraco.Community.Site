using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

/// <summary>
/// A Contentment data source that provides Sessionize event days for use in Data List editors.
/// </summary>
public class SessionizeDaysDataSource : IContentmentDataSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionizeDaysDataSource> _logger;

    public SessionizeDaysDataSource(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionizeDaysDataSource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Name => "Sessionize Days";

    public string Description => "Data source for available days from the configured Sessionize event.";

    public string Icon => "icon-calendar-alt";

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
            var days = sessionizeClient.GetEventDaysAsync().GetAwaiter().GetResult();

            return days.Select(d => new DataListItem
            {
                Name = d.DisplayName,
                Value = d.DateValue,
                Description = d.IsToday ? "Today" : null,
                Icon = "icon-calendar"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch event days from Sessionize for Contentment data source");
            return Enumerable.Empty<DataListItem>();
        }
    }
}

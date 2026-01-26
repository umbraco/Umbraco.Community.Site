using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

/// <summary>
/// A Contentment data source that provides Sessionize category items (tags) for use in Data List editors.
/// </summary>
public class SessionizeTagsDataSource : IContentmentDataSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionizeTagsDataSource> _logger;

    public SessionizeTagsDataSource(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionizeTagsDataSource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Name => "Sessionize Tags";

    public string Description => "Data source for category items (tags) from the configured Sessionize event.";

    public string Icon => "icon-tags";

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
            var categories = sessionizeClient.GetCategoriesAsync().GetAwaiter().GetResult();

            // Flatten all category items into a single list, grouped by category
            return categories
                .OrderBy(c => c.Sort)
                .SelectMany(c => c.Items
                    .OrderBy(i => i.Sort)
                    .Select(i => new DataListItem
                    {
                        Name = i.Name,
                        Value = i.Id.ToString(),
                        Description = c.Title,
                        Icon = "icon-tag"
                    }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch tags from Sessionize for Contentment data source");
            return Enumerable.Empty<DataListItem>();
        }
    }
}

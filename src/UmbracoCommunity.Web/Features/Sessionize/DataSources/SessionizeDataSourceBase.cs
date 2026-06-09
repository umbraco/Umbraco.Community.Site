using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

/// <summary>
/// Shared scaffold for Sessionize-backed Contentment data sources. Concrete sources
/// only need to declare their metadata and project the Sessionize payload into
/// <see cref="DataListItem"/>s.
/// </summary>
public abstract class SessionizeDataSourceBase : IContentmentDataSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected SessionizeDataSourceBase(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Icon { get; }

    public string Group => "Sessionize";
    public OverlaySize OverlaySize => OverlaySize.Small;
    public Dictionary<string, object>? DefaultValues => null;
    public IEnumerable<ContentmentConfigurationField> Fields => Enumerable.Empty<ContentmentConfigurationField>();

    public IEnumerable<DataListItem> GetItems(Dictionary<string, object> config)
    {
        try
        {
            // Create a scope to resolve the scoped SessionizeApiClient from this singleton data source.
            // The client caches in memory, so repeat calls are cheap.
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<SessionizeApiClient>();
            return FetchItems(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Source} from Sessionize for Contentment data source", Name);
            return Enumerable.Empty<DataListItem>();
        }
    }

    protected abstract IEnumerable<DataListItem> FetchItems(SessionizeApiClient client);
}

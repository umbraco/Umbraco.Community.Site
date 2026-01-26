using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

/// <summary>
/// A Contentment data source that provides Sessionize sessions for use in Data List editors.
/// </summary>
public class SessionizeSessionsDataSource : IContentmentDataSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionizeSessionsDataSource> _logger;

    public SessionizeSessionsDataSource(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionizeSessionsDataSource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Name => "Sessionize Sessions";

    public string Description => "Data source for sessions from the configured Sessionize event.";

    public string Icon => "icon-calendar";

    public string Group => "Sessionize";

    public OverlaySize OverlaySize => OverlaySize.Small;

    public Dictionary<string, object>? DefaultValues => null;

    public IEnumerable<ContentmentConfigurationField> Fields => Enumerable.Empty<ContentmentConfigurationField>();

    public IEnumerable<DataListItem> GetItems(Dictionary<string, object> config)
    {
        try
        {
            // Create a scope to resolve the scoped SessionizeApiClient from this singleton data source.
            // Using sync-over-async as Contentment data sources don't support async.
            // The SessionizeApiClient has caching built-in, so this typically returns quickly after first call.
            using var scope = _scopeFactory.CreateScope();
            var sessionizeClient = scope.ServiceProvider.GetRequiredService<SessionizeApiClient>();
            var sessions = sessionizeClient.GetSessionsAsync().GetAwaiter().GetResult();

            return sessions
                .Where(s => !s.IsServiceSession)
                .OrderBy(s => s.StartsAt)
                .ThenBy(s => s.Title)
                .Select(s => new DataListItem
                {
                    Name = FormatSessionName(s),
                    Value = s.Id,
                    Description = FormatSessionDescription(s),
                    Icon = "icon-presentation"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch sessions from Sessionize for Contentment data source");
            return Enumerable.Empty<DataListItem>();
        }
    }

    private static string FormatSessionName(Models.SessionizeSession session)
    {
        var speakers = session.Speakers?.Count > 0
            ? $" — {string.Join(", ", session.Speakers.Select(s => s.FullName))}"
            : string.Empty;

        return $"{session.Title}{speakers}";
    }

    private static string? FormatSessionDescription(Models.SessionizeSession session)
    {
        if (!session.StartsAt.HasValue)
            return session.Room;

        var time = session.StartsAt.Value.ToString("ddd d MMM, HH:mm");
        return string.IsNullOrEmpty(session.Room)
            ? time
            : $"{time} • {session.Room}";
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;
using UmbracoCommunity.Web.Features.Sessionize.Models;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

public class SessionizeSessionsDataSource : SessionizeDataSourceBase
{
    public SessionizeSessionsDataSource(IServiceScopeFactory scopeFactory, ILogger<SessionizeSessionsDataSource> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "Sessionize Sessions";
    public override string Description => "Data source for sessions from the configured Sessionize event.";
    public override string Icon => "icon-calendar";

    protected override IEnumerable<DataListItem> FetchItems(SessionizeApiClient client)
    {
        var sessions = client.GetSessionsAsync().GetAwaiter().GetResult();

        return sessions
            .Where(s => !s.IsServiceSession)
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.Title)
            .Select(s => new DataListItem
            {
                Name = FormatSessionName(s),
                Value = s.Id,
                Description = FormatSessionDescription(s),
                Icon = "icon-presentation",
            });
    }

    private static string FormatSessionName(SessionizeSession session)
    {
        var speakers = session.Speakers?.Count > 0
            ? $" — {string.Join(", ", session.Speakers.Select(s => s.FullName))}"
            : string.Empty;

        return $"{session.Title}{speakers}";
    }

    private static string? FormatSessionDescription(SessionizeSession session)
    {
        if (!session.StartsAt.HasValue)
            return session.Room;

        var time = session.StartsAt.Value.ToString("ddd d MMM, HH:mm");
        return string.IsNullOrEmpty(session.Room)
            ? time
            : $"{time} • {session.Room}";
    }
}

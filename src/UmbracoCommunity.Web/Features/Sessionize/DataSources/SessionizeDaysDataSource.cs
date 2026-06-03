using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

public class SessionizeDaysDataSource : SessionizeDataSourceBase
{
    public SessionizeDaysDataSource(IServiceScopeFactory scopeFactory, ILogger<SessionizeDaysDataSource> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "Sessionize Days";
    public override string Description => "Data source for available days from the configured Sessionize event.";
    public override string Icon => "icon-calendar-alt";

    protected override IEnumerable<DataListItem> FetchItems(SessionizeApiClient client)
    {
        var days = client.GetEventDaysAsync().GetAwaiter().GetResult();
        var today = ProgramSessionResolver.ToEventTime(DateTime.UtcNow).Date;

        return days.Select(d => new DataListItem
        {
            Name = d.DisplayName,
            Value = d.DateValue,
            Description = d.Date.Date == today ? "Today" : null,
            Icon = "icon-calendar",
        });
    }
}

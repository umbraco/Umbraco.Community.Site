using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

public class SessionizeRoomsDataSource : SessionizeDataSourceBase
{
    public SessionizeRoomsDataSource(IServiceScopeFactory scopeFactory, ILogger<SessionizeRoomsDataSource> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "Sessionize Rooms";
    public override string Description => "Data source for rooms from the configured Sessionize event.";
    public override string Icon => "icon-company";

    protected override IEnumerable<DataListItem> FetchItems(SessionizeApiClient client)
    {
        var rooms = client.GetRoomsAsync().GetAwaiter().GetResult();

        return rooms
            .OrderBy(r => r.Sort)
            .Select(r => new DataListItem
            {
                Name = r.Name,
                Value = r.Id.ToString(),
                Icon = "icon-company",
            });
    }
}

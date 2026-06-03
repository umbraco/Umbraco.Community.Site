using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Community.Contentment.DataEditors;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

namespace UmbracoCommunity.Web.Features.Sessionize.DataSources;

public class SessionizeTagsDataSource : SessionizeDataSourceBase
{
    public SessionizeTagsDataSource(IServiceScopeFactory scopeFactory, ILogger<SessionizeTagsDataSource> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "Sessionize Tags";
    public override string Description => "Data source for category items (tags) from the configured Sessionize event.";
    public override string Icon => "icon-tags";

    protected override IEnumerable<DataListItem> FetchItems(SessionizeApiClient client)
    {
        var categories = client.GetCategoriesAsync().GetAwaiter().GetResult();

        return categories
            .OrderBy(c => c.Sort)
            .SelectMany(c => c.Items
                .OrderBy(i => i.Sort)
                .Select(i => new DataListItem
                {
                    Name = i.Name,
                    Value = i.Id.ToString(),
                    Description = c.Title,
                    Icon = "icon-tag",
                }));
    }
}

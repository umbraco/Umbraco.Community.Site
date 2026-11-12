using Examine;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoCommunity.Web.Services;

namespace UmbracoCommunity.Web.Notifications;

/// <summary>Cleans the block fields site search reads before the external index stores them.</summary>
public sealed class SearchIndexBlockTextHandler : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private readonly IExamineManager _examineManager;

    public SearchIndexBlockTextHandler(IExamineManager examineManager) => _examineManager = examineManager;

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        if (_examineManager.TryGetIndex(Umbraco.Cms.Core.Constants.UmbracoIndexes.ExternalIndexName, out var index)
            && index is BaseIndexProvider provider)
        {
            provider.TransformingIndexValues += OnTransformingIndexValues;
        }
    }

    private static void OnTransformingIndexValues(object? sender, IndexingItemEventArgs e)
    {
        if (!SearchService.BlockFields.Any(e.ValueSet.Values.ContainsKey)) return;

        var values = e.ValueSet.Values.ToDictionary(
            field => field.Key,
            field => SearchService.BlockFields.Contains(field.Key)
                ? field.Value.Select(value => value is string text ? SearchIndexText.Clean(text) : value)
                : field.Value.AsEnumerable());

        e.SetValues(values);
    }
}

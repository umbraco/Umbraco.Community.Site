using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Features.YouTube;

/// <summary>
/// Refuses saves that add a new legacy Video block. Existing ones keep working: they can't simply be removed from
/// the block editors, because Umbraco then flags them as invalid and editors couldn't save those pages at all.
/// </summary>
public sealed class LegacyVideoBlockGuard : INotificationHandler<ContentSavingNotification>
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;

    public LegacyVideoBlockGuard(IContentTypeService contentTypeService, IContentService contentService)
    {
        _contentTypeService = contentTypeService;
        _contentService = contentService;
    }

    public void Handle(ContentSavingNotification notification)
    {
        if (_contentTypeService.Get(VideoBlock.ModelTypeAlias)?.Key is not { } legacyTypeKey) return;

        foreach (var content in notification.SavedEntities)
        {
            var current = BlockKeys(content, legacyTypeKey);
            if (current.Count == 0) continue;

            var previous = content.HasIdentity && _contentService.GetById(content.Id) is { } saved
                ? BlockKeys(saved, legacyTypeKey)
                : [];

            if (current.Except(previous).Any())
            {
                notification.CancelOperation(new EventMessage(
                    "Use the Video block",
                    "The legacy Video block can't be added any more. Add a Video block instead; legacy videos already on this page are kept.",
                    EventMessageType.Error));
                return;
            }
        }
    }

    private static HashSet<string> BlockKeys(IContentBase content, Guid blockTypeKey)
        => BlockKeys(content.Properties.SelectMany(p => p.Values).Select(v => v.EditedValue).OfType<string>(), blockTypeKey);

    internal static HashSet<string> BlockKeys(IEnumerable<string> propertyValues, Guid blockTypeKey)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var typeKey = blockTypeKey.ToString();

        foreach (var json in propertyValues)
        {
            if (!json.Contains(typeKey, StringComparison.OrdinalIgnoreCase)) continue;
            Collect(Parse(json), typeKey, keys);
        }

        return keys;
    }

    // Blocks nested in another block's property can be stored as a serialised string, so strings are parsed too.
    private static void Collect(JsonNode? node, string typeKey, HashSet<string> keys)
    {
        switch (node)
        {
            case JsonObject item:
                if (string.Equals(Text(item["contentTypeKey"]), typeKey, StringComparison.OrdinalIgnoreCase) && Text(item["key"]) is { } key)
                {
                    keys.Add(key);
                }

                foreach (var (_, child) in item) Collect(child, typeKey, keys);
                break;

            case JsonArray array:
                foreach (var child in array) Collect(child, typeKey, keys);
                break;

            case JsonValue when Text(node) is { } text && text.TrimStart().StartsWith('{') && text.Contains(typeKey, StringComparison.OrdinalIgnoreCase):
                Collect(Parse(text), typeKey, keys);
                break;
        }
    }

    private static JsonNode? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

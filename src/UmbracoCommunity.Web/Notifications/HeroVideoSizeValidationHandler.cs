using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Notifications;

public class HeroVideoSizeValidationHandler :
    INotificationHandler<ContentSavingNotification>,
    INotificationHandler<ContentPublishingNotification>
{
    private static readonly string[] VideoPropertyAliases = ["video", "mobileVideo"];

    private readonly IContentTypeService _contentTypeService;
    private readonly IMediaService _mediaService;

    public HeroVideoSizeValidationHandler(IContentTypeService contentTypeService, IMediaService mediaService)
    {
        _contentTypeService = contentTypeService;
        _mediaService = mediaService;
    }

    public void Handle(ContentSavingNotification notification)
        => Validate(notification.SavedEntities, notification.CancelOperation);

    public void Handle(ContentPublishingNotification notification)
        => Validate(notification.PublishedEntities, notification.CancelOperation);

    private void Validate(IEnumerable<IContent> entities, Action<EventMessage> cancel)
    {
        var heroTypeKey = _contentTypeService.Get(HeroBannerWithVideo.ModelTypeAlias)?.Key;
        if (heroTypeKey is null)
        {
            return;
        }

        var heroTypeKeyText = heroTypeKey.Value.ToString();
        var mediaKeys = entities
            .SelectMany(content => content.Properties)
            .SelectMany(property => property.Values)
            .Select(value => value.EditedValue)
            .OfType<string>()
            .Where(json => json.Contains(heroTypeKeyText, StringComparison.OrdinalIgnoreCase))
            .SelectMany(json => FindVideoMediaKeys(json, heroTypeKey.Value))
            .Distinct();

        foreach (var mediaKey in mediaKeys)
        {
            var media = _mediaService.GetById(mediaKey);
            var bytes = media?.GetValue<long>(Umbraco.Cms.Core.Constants.Conventions.Media.Bytes) ?? 0;
            if (bytes <= Constants.Media.MaxHeroVideoBytes)
            {
                continue;
            }

            cancel(new EventMessage(
                "Video too large",
                $"\"{media!.Name}\" is {Constants.Media.FormatMegabytes(bytes)}. Hero videos can be at most {Constants.Media.FormatMegabytes(Constants.Media.MaxHeroVideoBytes)}, so compress it or use a shorter clip.",
                EventMessageType.Error));
            return;
        }
    }

    private static List<Guid> FindVideoMediaKeys(string json, Guid heroTypeKey)
    {
        var keys = new List<Guid>();
        try
        {
            CollectVideoMediaKeys(JsonNode.Parse(json), heroTypeKey, keys);
        }
        catch (JsonException)
        {
        }

        return keys;
    }

    private static void CollectVideoMediaKeys(JsonNode? node, Guid heroTypeKey, List<Guid> keys)
    {
        switch (node)
        {
            case JsonObject block:
                if (Guid.TryParse(AsString(block["contentTypeKey"]), out var typeKey)
                    && typeKey == heroTypeKey
                    && block["values"] is JsonArray values)
                {
                    foreach (var value in values.OfType<JsonObject>())
                    {
                        if (!VideoPropertyAliases.Contains(AsString(value["alias"])) || AsArray(value["value"]) is not { } items)
                        {
                            continue;
                        }

                        foreach (var item in items.OfType<JsonObject>())
                        {
                            if (Guid.TryParse(AsString(item["mediaKey"]), out var mediaKey))
                            {
                                keys.Add(mediaKey);
                            }
                        }
                    }
                }

                foreach (var (_, child) in block)
                {
                    CollectVideoMediaKeys(child, heroTypeKey, keys);
                }
                break;

            case JsonArray array:
                foreach (var child in array)
                {
                    CollectVideoMediaKeys(child, heroTypeKey, keys);
                }
                break;
        }
    }

    private static string? AsString(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    // Persisted block values hold the media picker value as a serialised string; editor payloads hold an array.
    private static JsonArray? AsArray(JsonNode? node)
        => node as JsonArray ?? (AsString(node) is { } text && text.TrimStart().StartsWith('[') ? JsonNode.Parse(text) as JsonArray : null);
}

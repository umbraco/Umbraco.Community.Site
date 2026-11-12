using System.Text.Json;
using System.Text.Json.Nodes;

namespace UmbracoCommunity.Web.Features.YouTube;

/// <summary>What the YouTube video picker stores: the pasted link, its video id and title, and the poster's media key.</summary>
public sealed class YouTubeVideoValue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string? Url { get; set; }

    public string? VideoId { get; set; }

    public string? Title { get; set; }

    public Guid? PosterKey { get; set; }

    /// <summary>Reads a stored value, which arrives as a JSON string, a JSON node or element, or already deserialised.</summary>
    public static YouTubeVideoValue? TryParse(object? source)
    {
        try
        {
            return source switch
            {
                null => null,
                YouTubeVideoValue value => value,
                string json when string.IsNullOrWhiteSpace(json) => null,
                string json => JsonSerializer.Deserialize<YouTubeVideoValue>(json, JsonOptions),
                JsonElement element when element.ValueKind == JsonValueKind.Object => element.Deserialize<YouTubeVideoValue>(JsonOptions),
                JsonNode node => node.Deserialize<YouTubeVideoValue>(JsonOptions),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

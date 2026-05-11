using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public sealed record CalendarFeed(
    FeedMeta Feed,
    IReadOnlyList<CalendarEvent> Events);

public sealed record FeedMeta(
    string Title,
    string SourceUrl,
    DateTimeOffset GeneratedAt);

public sealed record CalendarEvent(
    string Id,
    string Url,
    string Title,
    string? Summary,
    DateTimeOffset PublishedAt,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Location,
    string? Organizer,
    AttendanceMode AttendanceMode,
    bool IsHqOrganized,
    bool IsCancelled);

public enum AttendanceMode
{
    Unknown = 0,
    InPerson,
    Online,
    Hybrid,
}

public static class CalendarFeedJsonOptions
{
    public static readonly JsonSerializerOptions Default = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new AttendanceModeConverter());
        return options;
    }

    private sealed class AttendanceModeConverter : JsonConverter<AttendanceMode>
    {
        public override AttendanceMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return value switch
            {
                "inPerson" => AttendanceMode.InPerson,
                "online" => AttendanceMode.Online,
                "hybrid" => AttendanceMode.Hybrid,
                _ => AttendanceMode.Unknown,
            };
        }

        public override void Write(Utf8JsonWriter writer, AttendanceMode value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value switch
            {
                AttendanceMode.InPerson => "inPerson",
                AttendanceMode.Online => "online",
                AttendanceMode.Hybrid => "hybrid",
                _ => "unknown",
            });
        }
    }
}

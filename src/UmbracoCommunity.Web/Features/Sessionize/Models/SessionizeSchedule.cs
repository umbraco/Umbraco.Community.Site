using System.Text.Json.Serialization;

namespace UmbracoCommunity.Web.Features.Sessionize.Models;

public class SessionizeSchedule
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("rooms")]
    public List<SessionizeRoom> Rooms { get; set; } = new();

    [JsonPropertyName("timeSlots")]
    public List<SessionizeTimeSlot> TimeSlots { get; set; } = new();
}

public class SessionizeRoom
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("session")]
    public SessionizeSession? Session { get; set; }
}

public class SessionizeTimeSlot
{
    [JsonPropertyName("slotStart")]
    public string SlotStart { get; set; } = string.Empty;

    [JsonPropertyName("rooms")]
    public List<SessionizeRoom> Rooms { get; set; } = new();
}

public class SessionizeCategory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<SessionizeCategoryItem> Items { get; set; } = new();

    [JsonPropertyName("sort")]
    public int Sort { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class SessionizeCategoryItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sort")]
    public int Sort { get; set; }
}

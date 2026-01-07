using System.Text.Json.Serialization;

namespace UmbracoCommunity.Web.Features.Sessionize.Models;

public class SessionizeSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("startsAt")]
    public DateTime? StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public DateTime? EndsAt { get; set; }

    [JsonPropertyName("isServiceSession")]
    public bool IsServiceSession { get; set; }

    [JsonPropertyName("isPlenumSession")]
    public bool IsPlenumSession { get; set; }

    [JsonPropertyName("speakers")]
    public List<SessionizeSpeaker> Speakers { get; set; } = new();

    [JsonPropertyName("roomId")]
    public int? RoomId { get; set; }

    [JsonPropertyName("room")]
    public string? Room { get; set; }

    [JsonPropertyName("liveUrl")]
    public string? LiveUrl { get; set; }

    [JsonPropertyName("recordingUrl")]
    public string? RecordingUrl { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("categoryItems")]
    public List<int> CategoryItems { get; set; } = new();
}

public class SessionizeSessionGroup
{
    [JsonPropertyName("groupId")]
    public int? GroupId { get; set; }

    [JsonPropertyName("groupName")]
    public string GroupName { get; set; } = string.Empty;

    [JsonPropertyName("sessions")]
    public List<SessionizeSession> Sessions { get; set; } = new();
}

public class SessionizeSessionOverview
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

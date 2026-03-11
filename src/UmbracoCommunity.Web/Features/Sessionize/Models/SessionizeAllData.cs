using System.Text.Json.Serialization;

namespace UmbracoCommunity.Web.Features.Sessionize.Models;

/// <summary>
/// Represents the complete data from Sessionize's "All" endpoint
/// </summary>
public class SessionizeAllData
{
    [JsonPropertyName("sessions")]
    public List<SessionizeSessionRaw> Sessions { get; set; } = new();

    [JsonPropertyName("speakers")]
    public List<SessionizeSpeakerRaw> Speakers { get; set; } = new();

    [JsonPropertyName("categories")]
    public List<SessionizeCategory> Categories { get; set; } = new();

    [JsonPropertyName("questions")]
    public List<SessionizeQuestion> Questions { get; set; } = new();

    [JsonPropertyName("rooms")]
    public List<SessionizeRoomInfo> Rooms { get; set; } = new();

    /// <summary>
    /// Cached question ID for the "Pronouns" question, resolved once after deserialization.
    /// </summary>
    [JsonIgnore]
    public int? PronounsQuestionId { get; set; }
}

/// <summary>
/// Raw session data from the "All" endpoint (speakers are IDs, not full objects)
/// </summary>
public class SessionizeSessionRaw
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
    public List<string> SpeakerIds { get; set; } = new();

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

/// <summary>
/// Raw speaker data from the "All" endpoint
/// </summary>
public class SessionizeSpeakerRaw
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("tagLine")]
    public string? TagLine { get; set; }

    [JsonPropertyName("profilePicture")]
    public string? ProfilePicture { get; set; }

    [JsonPropertyName("isTopSpeaker")]
    public bool IsTopSpeaker { get; set; }

    [JsonPropertyName("links")]
    public List<SessionizeLink> Links { get; set; } = new();

    [JsonPropertyName("sessions")]
    public List<int> SessionIds { get; set; } = new();

    [JsonPropertyName("categoryItems")]
    public List<int> CategoryItems { get; set; } = new();

    [JsonPropertyName("questionAnswers")]
    public List<SessionizeQuestionAnswer> QuestionAnswers { get; set; } = new();
}

/// <summary>
/// Room information from the "All" endpoint
/// </summary>
public class SessionizeRoomInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sort")]
    public int Sort { get; set; }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmbracoCommunity.MeetBooking.Models;

/// <summary>
/// Mirrors the <c>state</c> object the Apps Script returns and accepts back. Persisted as JSON on the record so a
/// re-run of the workflow resumes where the previous attempt stopped instead of creating a second event.
/// </summary>
public sealed class MeetProvisioningState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonPropertyName("eventId")] public string? EventId { get; set; }
    [JsonPropertyName("htmlLink")] public string? HtmlLink { get; set; }
    [JsonPropertyName("meetingCode")] public string? MeetingCode { get; set; }
    [JsonPropertyName("meetUri")] public string? MeetUri { get; set; }
    [JsonPropertyName("spaceName")] public string? SpaceName { get; set; }
    [JsonPropertyName("configured")] public bool Configured { get; set; }
    [JsonPropertyName("cohostAdded")] public bool CohostAdded { get; set; }
    [JsonPropertyName("cohostManual")] public bool CohostManual { get; set; }

    /// <summary>
    /// The script deliberately did not touch co-hosts (<c>COHOST_MODE=off</c>), because presenting is unrestricted
    /// and so no co-host is required. Distinct from <see cref="CohostManual"/>, which means a human still owes us
    /// one, and it counts towards <see cref="IsComplete"/> — otherwise a finished booking would look unfinished
    /// forever and every re-run would call Google again.
    /// </summary>
    [JsonPropertyName("cohostSkipped")] public bool CohostSkipped { get; set; }
    [JsonPropertyName("cohostInstructions")] public string? CohostInstructions { get; set; }

    /// <summary>
    /// Why the co-host API call was abandoned, when the script was running in <c>auto</c> mode and fell back to
    /// manual. Null in <c>manual</c> mode — nothing was attempted — and null once a co-host has been added.
    /// </summary>
    [JsonPropertyName("cohostError")] public string? CohostError { get; set; }

    /// <summary>
    /// True when every step the script performs has resolved: the co-host was added, handed to a human, or
    /// deliberately skipped. All three are terminal — only "no outcome at all" means unfinished.
    /// </summary>
    [JsonIgnore]
    public bool IsComplete =>
        EventId is not null && SpaceName is not null && Configured && (CohostAdded || CohostManual || CohostSkipped);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Null for empty or unparseable input — a corrupt hidden field must never block a retry.</summary>
    public static MeetProvisioningState? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<MeetProvisioningState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

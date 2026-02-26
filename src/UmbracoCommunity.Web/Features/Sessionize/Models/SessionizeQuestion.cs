using System.Text.Json.Serialization;

namespace UmbracoCommunity.Web.Features.Sessionize.Models;

/// <summary>
/// Top-level question definition from the Sessionize "All" endpoint
/// </summary>
public class SessionizeQuestion
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("questionType")]
    public string QuestionType { get; set; } = string.Empty;

    [JsonPropertyName("sort")]
    public int Sort { get; set; }
}

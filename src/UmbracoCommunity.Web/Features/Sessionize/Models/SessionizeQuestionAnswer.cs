using System.Text.Json.Serialization;

namespace UmbracoCommunity.Web.Features.Sessionize.Models;

/// <summary>
/// A speaker's answer to a question, from the speaker's questionAnswers array
/// </summary>
public class SessionizeQuestionAnswer
{
    [JsonPropertyName("questionId")]
    public int QuestionId { get; set; }

    [JsonPropertyName("answerValue")]
    public string? AnswerValue { get; set; }
}

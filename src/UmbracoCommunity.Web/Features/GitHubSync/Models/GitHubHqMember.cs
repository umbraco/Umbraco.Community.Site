using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace UmbracoCommunity.Web.Features.GitHubSync.Models;

public class GitHubHqMember
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("periods")]
    public List<EmploymentPeriod> Periods { get; set; } = new();
}

public class EmploymentPeriod
{
    [JsonPropertyName("start")]
    public DateTime? Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime? End { get; set; }
}

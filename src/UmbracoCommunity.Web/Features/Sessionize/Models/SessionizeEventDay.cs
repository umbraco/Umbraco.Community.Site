namespace UmbracoCommunity.Web.Features.Sessionize.Models;

/// <summary>
/// Represents a day of the event with sessions.
/// </summary>
public class SessionizeEventDay
{
    /// <summary>
    /// The date of the event day.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// The date formatted as ISO 8601 (yyyy-MM-dd) for use as a value.
    /// </summary>
    public string DateValue => Date.ToString("yyyy-MM-dd");

    /// <summary>
    /// A human-readable display name for the day (e.g., "Wednesday 5 March").
    /// </summary>
    public string DisplayName => Date.ToString("dddd d MMMM");
}

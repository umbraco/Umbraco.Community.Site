namespace UmbracoCommunity.BlogAnnouncements.Automate;

/// <summary>Output produced by <see cref="TriggerAutomationAction"/>.</summary>
public sealed class TriggerAutomationOutput
{
    /// <summary>The ID of the run created on the target automation.</summary>
    public Guid RunId { get; init; }
}

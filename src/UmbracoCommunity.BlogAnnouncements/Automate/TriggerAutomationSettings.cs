using Umbraco.Automate.Core.Settings;

namespace UmbracoCommunity.BlogAnnouncements.Automate;

/// <summary>Settings for <see cref="TriggerAutomationAction"/>.</summary>
public sealed class TriggerAutomationSettings
{
    /// <summary>The GUID of the automation to trigger.</summary>
    [Field(
        Label = "Automation ID",
        Description = "The GUID of the automation to trigger (open it in the Automations tree and copy the ID from the URL).",
        SupportsBindings = true)]
    public string AutomationId { get; set; } = string.Empty;
}

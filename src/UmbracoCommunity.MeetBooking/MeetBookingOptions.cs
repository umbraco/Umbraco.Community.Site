namespace UmbracoCommunity.MeetBooking;

/// <summary>
/// Configuration for the community Meet booking workflow, bound to the <c>MeetBooking</c> section.
/// Secrets (<see cref="AppsScriptOptions.WebAppUrl"/>, <see cref="AppsScriptOptions.SharedSecret"/>) ship
/// empty in <c>appsettings.json</c> and are supplied via <c>appsettings.Local.json</c> or Cloud portal
/// environment variables — never the repo.
/// </summary>
public sealed class MeetBookingOptions
{
    public const string SectionName = "MeetBooking";

    /// <summary>When false the workflow completes without doing anything. Kill switch.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Log what would be sent to the Apps Script and write a placeholder result; call nothing. For cutover.</summary>
    public bool DryRun { get; set; }

    public AppsScriptOptions AppsScript { get; set; } = new();

    public sealed class AppsScriptOptions
    {
        /// <summary>The API deployment's <c>/exec</c> URL of the community Meet booking Apps Script.</summary>
        public string WebAppUrl { get; set; } = "";

        /// <summary>Must equal the script's <c>SHARED_SECRET</c> script property.</summary>
        public string SharedSecret { get; set; } = "";

        /// <summary>Apps Script cold starts plus four Google calls; 30 seconds is too tight.</summary>
        public int TimeoutSeconds { get; set; } = 60;
    }
}

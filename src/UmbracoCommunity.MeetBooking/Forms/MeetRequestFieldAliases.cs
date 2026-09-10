namespace UmbracoCommunity.MeetBooking.Forms;

/// <summary>
/// The field aliases the workflow reads from and writes to on the Umbraco Form. These are fixed: an alias is set
/// once when the field is created in the backoffice and Forms never regenerates it, so there is nothing to
/// configure. If a form ever ends up with different ones, fix the form rather than the code — the README's table
/// is the contract.
/// </summary>
public static class MeetRequestFieldAliases
{
    // Read from the entry.
    public const string RequesterName = "requesterName";
    public const string CohostEmail = "googleEmail";

    /// <summary>Required. Used verbatim as the Calendar event's title.</summary>
    public const string Title = "meetingTitle";

    /// <summary>Optional detail for the event body. Most requests don't need it.</summary>
    public const string Description = "meetingDescription";

    public const string Date = "meetingDate";
    public const string StartTime = "startTime";
    public const string DurationMinutes = "durationMinutes";
    public const string TimeZone = "timeZone";
    public const string RecordMeeting = "recordMeeting";

    /// <summary>
    /// Opt-in for the Gemini "take notes for me" transcript and the plain transcript. One checkbox for both,
    /// because they are one editorial decision — and because they are independent switches in the Meet API, so
    /// leaving one implicit would silently produce notes for a meeting nobody asked to have written down.
    /// </summary>
    public const string TranscribeMeeting = "transcribeMeeting";

    // Written back. All are truncated to 255 characters — Forms stores a Hidden field's value in nvarchar(255).
    // The machine-readable provisioning state is not a form field at all; it lives in the MeetProvisioningStates
    // table (see Storage/IMeetStateStore.cs).
    public const string MeetLink = "meetLink";
    public const string ProvisioningError = "provisioningError";

    /// <summary>
    /// Written when the Apps Script hands the co-host step back to a human (<c>COHOST_MODE=manual</c>, or the
    /// Developer Preview members API being unavailable). Empty otherwise. On the entry rather than in the state
    /// store because it is a to-do for the approver: it has to show in the backoffice and the Slack post.
    /// </summary>
    public const string CohostAction = "cohostAction";

    /// <summary>Every alias above, for tests and diagnostics.</summary>
    public static readonly string[] All =
    [
        RequesterName, CohostEmail, Title, Description, Date, StartTime, DurationMinutes, TimeZone,
        RecordMeeting, TranscribeMeeting, MeetLink, ProvisioningError, CohostAction,
    ];
}

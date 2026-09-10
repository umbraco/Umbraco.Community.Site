using UmbracoCommunity.MeetBooking.Models;

namespace UmbracoCommunity.MeetBooking.Storage;

/// <summary>
/// Where a request's provisioning state lives between attempts. Behind an interface so the workflow is testable
/// without a database, and so the store can move again without touching the workflow.
/// </summary>
public interface IMeetStateStore
{
    /// <summary>The state left by a previous attempt, or null when there was none (or it is unreadable).</summary>
    MeetProvisioningState? Read(Guid recordId);

    /// <summary>
    /// Records the outcome of an attempt. Called <em>before</em> the entry's own fields are updated, so that a
    /// failure to write the form fields can never lose the knowledge that a Meet already exists.
    /// </summary>
    void Write(Guid recordId, MeetProvisioningState state, string? error);
}

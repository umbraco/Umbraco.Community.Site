using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UmbracoCommunity.MeetBooking.Models;

namespace UmbracoCommunity.MeetBooking.Storage;

/// <summary>
/// Stores provisioning state in the RCL's own table.
/// </summary>
/// <remarks>
/// Uses <see cref="IDbContextFactory{TContext}"/> so every call gets its own context and connection. That matters:
/// the workflow runs inside Umbraco's ambient scope, and a failed command on that scope's connection poisons the
/// whole request. Keeping this on a separate connection means a problem here surfaces as a logged error, not a 500
/// on the approve.
/// </remarks>
public sealed class EfMeetStateStore(
    IDbContextFactory<MeetBookingDbContext> contextFactory,
    TimeProvider clock,
    ILogger<EfMeetStateStore> logger) : IMeetStateStore
{
    public MeetProvisioningState? Read(Guid recordId)
    {
        try
        {
            using var db = contextFactory.CreateDbContext();
            var row = db.MeetProvisioningStates.AsNoTracking().FirstOrDefault(x => x.RecordId == recordId);
            return row is null ? null : MeetProvisioningState.FromJson(row.StateJson);
        }
        catch (Exception ex)
        {
            // Treated as "no previous state", so the workflow tries a fresh provisioning. The Apps Script's own
            // record-id tagging (adoptExistingEvent_) is what stops that becoming a duplicate event.
            logger.LogError(ex, "Could not read provisioning state for record {RecordId}; treating it as absent", recordId);
            return null;
        }
    }

    public void Write(Guid recordId, MeetProvisioningState state, string? error)
    {
        try
        {
            using var db = contextFactory.CreateDbContext();
            var row = db.MeetProvisioningStates.FirstOrDefault(x => x.RecordId == recordId);
            if (row is null)
            {
                row = new MeetProvisioningStateEntity { RecordId = recordId };
                db.MeetProvisioningStates.Add(row);
            }

            row.StateJson = state.ToJson();
            row.LastError = error;
            row.UpdatedUtc = clock.GetUtcNow().UtcDateTime;
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Log the state verbatim: if the Meet exists but we could not record it, this line is the only trace of
            // the event id, and a retry would otherwise create a duplicate.
            logger.LogError(ex, "Could not save provisioning state for record {RecordId}. State: {State}", recordId, state.ToJson());
        }
    }
}

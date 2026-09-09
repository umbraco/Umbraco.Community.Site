namespace UmbracoCommunity.MeetBooking.Models;

/// <summary>What the requester asked for, typed. Built from a Forms record by <see cref="Forms.MeetRequestRecordMapper"/>.</summary>
/// <param name="Title">The Calendar event's summary; the Apps Script rejects anything under 3 characters.</param>
/// <param name="StartLocal">Wall-clock start in <paramref name="TimeZone"/>; <see cref="DateTimeKind.Unspecified"/>.</param>
/// <param name="TimeZone">IANA id, e.g. <c>Europe/London</c>.</param>
public sealed record MeetRequest(
    Guid RecordId,
    string RequesterName,
    string CohostEmail,
    string Title,
    string Description,
    DateTime StartLocal,
    int DurationMinutes,
    string TimeZone,
    bool RecordMeeting,
    bool TranscribeMeeting);

/// <summary>Outcome of one call to the Apps Script. <see cref="State"/> is always present — partial on failure.</summary>
public sealed record ProvisionResult(bool Ok, string? Step, string? Error, MeetProvisioningState State)
{
    public static ProvisionResult Success(MeetProvisioningState state) => new(true, null, null, state);

    public static ProvisionResult Failure(string step, string error, MeetProvisioningState? state = null) =>
        new(false, step, error, state ?? new MeetProvisioningState());
}

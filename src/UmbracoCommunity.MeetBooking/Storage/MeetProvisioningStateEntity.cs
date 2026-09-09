namespace UmbracoCommunity.MeetBooking.Storage;

/// <summary>
/// The provisioning state for one Forms entry, keyed by the record's unique id.
/// </summary>
/// <remarks>
/// This lives in its own table rather than on a hidden Forms field because a Forms <c>Hidden</c> field has
/// <c>FieldDataType.String</c>, which Forms stores in <c>UFRecordDataString.Value</c> — <c>nvarchar(255)</c>. A real
/// state object is ~600 characters (the Calendar <c>htmlLink</c> alone is over 100), so writing it there throws
/// "String or binary data would be truncated", and because that SQL runs inside Umbraco's ambient scope the failure
/// poisons the whole request — the approve returns 500 and the entry never leaves <c>Submitted</c>. Catching the
/// exception does not help; the scope is already broken. See
/// <c>docs/plans/2026-09-04-community-meet-booking.md</c>, "Task 13".
/// </remarks>
public sealed class MeetProvisioningStateEntity
{
    /// <summary>The Forms record's <c>UniqueId</c>. Primary key — one row per entry.</summary>
    public Guid RecordId { get; set; }

    /// <summary>The Apps Script's <c>state</c> object, serialised. Unbounded: the contract may grow.</summary>
    public string StateJson { get; set; } = "";

    /// <summary>The last failure, as <c>step: error</c>, or null after a success. Untruncated, unlike the form field.</summary>
    public string? LastError { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

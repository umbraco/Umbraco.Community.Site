using System.Globalization;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using UmbracoCommunity.MeetBooking.Models;

namespace UmbracoCommunity.MeetBooking.Forms;

/// <summary>A required form field is missing or malformed. The message names the alias so a misconfigured form is obvious from the workflow log.</summary>
public sealed class MeetRequestMappingException(string alias, string problem)
    : Exception($"Form field '{alias}' {problem}")
{
    public string Alias { get; } = alias;
}

/// <summary>
/// Translates between an Umbraco Forms <see cref="Record"/> and the typed <see cref="MeetRequest"/>, and reads /
/// writes the hidden result fields. Field values are handled as the objects Forms actually stores: at submit time
/// a date picker value is a <see cref="DateTime"/> and a checkbox a <see cref="bool"/>, but a record reloaded from
/// the database (a re-run from the backoffice) carries strings — so every accessor accepts both.
/// </summary>
public static class MeetRequestRecordMapper
{
    // ISO only. Culture-shaped strings ("05/09/2026") are ambiguous between day/month and are rejected on purpose.
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss",
    ];

    /// <summary>Builds the request. Throws <see cref="MeetRequestMappingException"/> naming the alias when a required field is missing or malformed.</summary>
    public static MeetRequest Map(Record record)
    {

        var date = RequiredDate(record, MeetRequestFieldAliases.Date);
        var time = RequiredTime(record, MeetRequestFieldAliases.StartTime);
        var startLocal = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);

        var duration = RequiredInt(record, MeetRequestFieldAliases.DurationMinutes);
        if (duration is < 5 or > 480)
            throw new MeetRequestMappingException(MeetRequestFieldAliases.DurationMinutes, $"must be 5–480 minutes, got {duration}");

        var timeZone = RequiredString(record, MeetRequestFieldAliases.TimeZone);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var tz) || !tz.HasIanaId)
            throw new MeetRequestMappingException(MeetRequestFieldAliases.TimeZone, $"'{timeZone}' is not a known IANA time zone");
        if (tz.IsInvalidTime(startLocal))
            throw new MeetRequestMappingException(MeetRequestFieldAliases.StartTime, $"{startLocal:yyyy-MM-dd HH:mm} does not exist in {timeZone} (clocks go forward then)");

        return new MeetRequest(
            RecordId: record.UniqueId,
            RequesterName: RequiredString(record, MeetRequestFieldAliases.RequesterName),
            CohostEmail: RequiredString(record, MeetRequestFieldAliases.CohostEmail),
            Title: RequiredString(record, MeetRequestFieldAliases.Title),
            Description: OptionalString(record, MeetRequestFieldAliases.Description) ?? "",
            StartLocal: startLocal,
            DurationMinutes: duration,
            TimeZone: timeZone,
            // Absent or blank means don't record. Recording someone's meeting they didn't ask for is the worse
            // failure, so a missing field must not silently switch it on.
            RecordMeeting: OptionalBool(record, MeetRequestFieldAliases.RecordMeeting) ?? false,
            // Same reasoning for notes and the transcript: a missing field must not switch them on. Also keeps the
            // workflow working against a form that predates the field.
            TranscribeMeeting: OptionalBool(record, MeetRequestFieldAliases.TranscribeMeeting) ?? false);
    }

    /// <summary>The last error written by <see cref="WriteResult"/>, if any.</summary>
    public static string? ReadError(Record record) => OptionalString(record, MeetRequestFieldAliases.ProvisioningError);

    /// <summary>
    /// Writes the human-facing outcome onto the record's hidden fields (in memory — the caller persists), so the
    /// approver and the Slack workflow that runs after this one can see it. Fields missing from the record but
    /// present on the form are created; the created <see cref="RecordField"/>s are returned because the persister has
    /// to insert rather than update them.
    /// </summary>
    /// <remarks>
    /// The machine-readable state is <em>not</em> written here — it lives in
    /// <see cref="Storage.IMeetStateStore"/>. A Hidden field is stored in <c>UFRecordDataString.Value</c>, which is
    /// <c>nvarchar(255)</c>; every value written here is truncated to fit, because an oversized write throws inside
    /// Umbraco's ambient scope and takes the whole approve request down with it.
    /// </remarks>
    public static IReadOnlyList<RecordField> WriteResult(Record record, Form form, ProvisionResult result)
    {
        var created = new List<RecordField>();
        SetValue(record, form, MeetRequestFieldAliases.MeetLink, result.State.MeetUri ?? "", created);
        SetValue(record, form, MeetRequestFieldAliases.ProvisioningError, ProvisioningErrorFor(result), created);
        SetValue(record, form, MeetRequestFieldAliases.CohostAction, CohostActionFor(result.State), created);
        return created;
    }

    /// <summary>
    /// What to show on the entry as having gone wrong: the failed step, or — on a booking that otherwise succeeded —
    /// the co-host API error that made the script hand that step to a human. Empty when nothing went wrong.
    /// </summary>
    private static string ProvisioningErrorFor(ProvisionResult result)
    {
        if (!result.Ok) return $"{result.Step}: {result.Error}";
        return string.IsNullOrWhiteSpace(result.State.CohostError) ? "" : $"cohost: {result.State.CohostError}";
    }

    /// <summary>
    /// The approver's outstanding co-host task, or empty when there is none. The script's own wording is used when it
    /// supplied any, so the instructions stay in one place.
    /// </summary>
    private static string CohostActionFor(MeetProvisioningState state)
    {
        if (state.CohostAdded || !state.CohostManual) return "";
        return string.IsNullOrWhiteSpace(state.CohostInstructions)
            ? "Add the requester as co-host on the Meet before the meeting starts."
            : state.CohostInstructions;
    }

    /// <summary>Forms stores a Hidden field's value in <c>nvarchar(255)</c>; anything longer fails the whole request.</summary>
    internal const int MaxFormFieldLength = 255;

    private static string Truncate(string value) =>
        value.Length <= MaxFormFieldLength ? value : value[..(MaxFormFieldLength - 1)] + "…";

    // --- reading -----------------------------------------------------------------------------------------------

    private static object? FirstValue(Record record, string alias)
    {
        var field = record.GetRecordFieldByAlias(alias);
        var value = field?.Values?.FirstOrDefault();
        return value is string s && string.IsNullOrWhiteSpace(s) ? null : value;
    }

    private static string? OptionalString(Record record, string alias) =>
        FirstValue(record, alias) switch
        {
            null => null,
            DateTime d => d.ToString("s", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            var v => Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim(),
        };

    private static string RequiredString(Record record, string alias) =>
        OptionalString(record, alias) ?? throw new MeetRequestMappingException(alias, "is required but empty");

    private static int RequiredInt(Record record, string alias)
    {
        var raw = RequiredString(record, alias);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i
            : throw new MeetRequestMappingException(alias, $"'{raw}' is not a whole number");
    }

    private static bool? OptionalBool(Record record, string alias) =>
        FirstValue(record, alias) switch
        {
            null => null,
            bool b => b,
            var v => Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant() is "true" or "on" or "1" or "yes",
        };

    private static DateTime RequiredDate(Record record, string alias)
    {
        var value = FirstValue(record, alias) ?? throw new MeetRequestMappingException(alias, "is required but empty");
        if (value is DateTime dt) return dt;
        var raw = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (DateTime.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return parsed;
        throw new MeetRequestMappingException(alias, $"'{raw}' is not an ISO date (yyyy-MM-dd)");
    }

    private static TimeSpan RequiredTime(Record record, string alias)
    {
        var raw = RequiredString(record, alias);
        return TimeSpan.TryParseExact(raw, [@"hh\:mm", @"h\:mm", @"hh\:mm\:ss"], CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1)
            ? t
            : throw new MeetRequestMappingException(alias, $"'{raw}' is not a time of day (HH:mm)");
    }

    // --- writing -----------------------------------------------------------------------------------------------

    private static void SetValue(Record record, Form form, string alias, string value, List<RecordField> created)
    {
        var recordField = record.GetRecordFieldByAlias(alias);
        if (recordField is null)
        {
            var formField = form.AllFields.FirstOrDefault(f => string.Equals(f.Alias, alias, StringComparison.OrdinalIgnoreCase))
                ?? throw new MeetRequestMappingException(alias, "hidden field is missing from the form — add it (see README)");
            recordField = new RecordField(formField) { Record = record.Id };
            record.RecordFields[formField.Id] = recordField;
            created.Add(recordField);
        }

        recordField.Values = [Truncate(value)];
    }
}

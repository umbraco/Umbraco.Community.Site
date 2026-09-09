using Microsoft.Extensions.Options;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using UmbracoCommunity.MeetBooking.Forms;
using UmbracoCommunity.MeetBooking.Models;

namespace UmbracoCommunity.MeetBooking.Tests;

/// <summary>Hand-rolled fixtures: a form + record with the default field aliases, and an options monitor.</summary>
internal static class TestHelpers
{
    public static readonly Guid RecordId = new("11111111-2222-3333-4444-555555555555");
    public static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    public static IOptionsMonitor<MeetBookingOptions> Options(Action<MeetBookingOptions>? configure = null)
    {
        var opts = new MeetBookingOptions();
        configure?.Invoke(opts);
        return new StaticOptionsMonitor(opts);
    }


    /// <summary>A form carrying every default alias, including the hidden write-back fields.</summary>
    public static Form Form(params string[] omitAliases)
    {
        var aliases = MeetRequestFieldAliases.All;
        var form = new Form { Id = Guid.NewGuid(), Name = "Community Meet request" };
        var page = new Page { Caption = "Page 1" };
        var fieldset = new FieldSet { Id = Guid.NewGuid() };
        var container = new FieldsetContainer { Width = 12 };
        foreach (var alias in aliases.Where(x => !omitAliases.Contains(x)))
            container.Fields.Add(new Field { Id = Guid.NewGuid(), Alias = alias, Caption = alias, Settings = new Dictionary<string, string>() });
        fieldset.Containers.Add(container);
        page.FieldSets.Add(fieldset);
        form.Pages.Add(page);
        return form;
    }

    /// <summary>A record for <paramref name="form"/> with the given values (object values are stored as-is, like Forms does at submit time).</summary>
    public static Record Record(Form form, IDictionary<string, object?> values)
    {
        var record = new Record { UniqueId = RecordId, Id = 42, Form = form.Id, Created = Now.UtcDateTime };
        foreach (var field in form.AllFields)
        {
            if (!values.TryGetValue(field.Alias, out var value)) continue;
            var rf = new RecordField(field) { Key = Guid.NewGuid(), Record = record.Id, Values = value is null ? [] : [value] };
            record.RecordFields[field.Id] = rf;
        }
        return record;
    }

    /// <summary>A complete, valid submission with typed values as Forms produces them at submit time.</summary>
    public static Dictionary<string, object?> ValidValues() => new()
    {
        ["requesterName"] = "Jane Doe",
        ["googleEmail"] = "jane@gmail.com",
        ["meetingTitle"] = "Umbraco London meetup",
        ["meetingDescription"] = "Monthly meetup, two talks.",
        ["meetingDate"] = new DateTime(2026, 9, 17),
        ["startTime"] = "19:00",
        ["durationMinutes"] = "90",
        ["timeZone"] = "Europe/London",
        ["recordMeeting"] = true,
        ["transcribeMeeting"] = true,
    };

    public static MeetRequest Request() => new(
        RecordId, "Jane Doe", "jane@gmail.com", "Umbraco London meetup", "Monthly meetup.",
        new DateTime(2026, 9, 17, 19, 0, 0), 90, "Europe/London", true, true);

    public static MeetProvisioningState CompleteState() => new()
    {
        EventId = "evt1", HtmlLink = "https://calendar.google.com/x", MeetingCode = "abc-defg-hij",
        MeetUri = "https://meet.google.com/abc-defg-hij", SpaceName = "spaces/XYZ", Configured = true, CohostAdded = true,
    };

    public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StaticOptionsMonitor(MeetBookingOptions value) : IOptionsMonitor<MeetBookingOptions>
    {
        public MeetBookingOptions CurrentValue => value;
        public MeetBookingOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<MeetBookingOptions, string?> listener) => null;
    }
}

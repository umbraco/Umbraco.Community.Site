using FluentAssertions;
using UmbracoCommunity.MeetBooking.Forms;
using UmbracoCommunity.MeetBooking.Models;
using Xunit;

namespace UmbracoCommunity.MeetBooking.Tests;

public class MeetRequestRecordMapperTests
{
    [Fact]
    public void Maps_a_complete_submission_with_typed_values()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());

        var request = MeetRequestRecordMapper.Map(record);

        request.RecordId.Should().Be(TestHelpers.RecordId);
        request.RequesterName.Should().Be("Jane Doe");
        request.CohostEmail.Should().Be("jane@gmail.com");
        request.Title.Should().Be("Umbraco London meetup");
        request.Description.Should().Be("Monthly meetup, two talks.");
        request.StartLocal.Should().Be(new DateTime(2026, 9, 17, 19, 0, 0));
        request.StartLocal.Kind.Should().Be(DateTimeKind.Unspecified);
        request.DurationMinutes.Should().Be(90);
        request.TimeZone.Should().Be("Europe/London");
        request.RecordMeeting.Should().BeTrue("the fixture ticks the box");
    }

    [Theory]
    [InlineData("2026-09-17")]
    [InlineData("2026-09-17T00:00:00")]
    [InlineData("2026-09-17 00:00:00")]
    public void Accepts_iso_string_dates_as_stored_after_a_reload(string stored)
    {
        var values = TestHelpers.ValidValues();
        values["meetingDate"] = stored;
        var request = MeetRequestRecordMapper.Map(TestHelpers.Record(TestHelpers.Form(), values));
        request.StartLocal.Should().Be(new DateTime(2026, 9, 17, 19, 0, 0));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("on", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("", false)]    // absent/blank → off; never record without being asked
    public void Reads_checkbox_variants(string stored, bool expected)
    {
        var values = TestHelpers.ValidValues();
        values["recordMeeting"] = stored;
        MeetRequestRecordMapper.Map(TestHelpers.Record(TestHelpers.Form(), values)).RecordMeeting.Should().Be(expected);
    }

    [Fact]
    public void Bool_false_object_turns_recording_off()
    {
        var values = TestHelpers.ValidValues();
        values["recordMeeting"] = false;
        MeetRequestRecordMapper.Map(TestHelpers.Record(TestHelpers.Form(), values)).RecordMeeting.Should().BeFalse();
    }

    [Theory]
    [InlineData("requesterName")]
    [InlineData("googleEmail")]
    [InlineData("meetingTitle")]
    [InlineData("meetingDate")]
    [InlineData("startTime")]
    [InlineData("durationMinutes")]
    [InlineData("timeZone")]
    public void Missing_required_field_throws_naming_the_alias(string alias)
    {
        var values = TestHelpers.ValidValues();
        values[alias] = null;

        var act = () => MeetRequestRecordMapper.Map(TestHelpers.Record(TestHelpers.Form(), values));

        act.Should().Throw<MeetRequestMappingException>().Which.Alias.Should().Be(alias);
    }

    [Theory]
    [InlineData("durationMinutes", "4")]
    [InlineData("durationMinutes", "481")]
    [InlineData("durationMinutes", "ninety")]
    [InlineData("startTime", "25:00")]
    [InlineData("startTime", "7pm")]
    [InlineData("timeZone", "Mars/Olympus")]
    [InlineData("timeZone", "Romance Standard Time")]   // Windows id — Apps Script wouldn't understand it
    [InlineData("meetingDate", "next thursday")]
    [InlineData("meetingDate", "05/09/2026")]           // ambiguous day/month — rejected on purpose
    public void Malformed_values_throw_naming_the_alias(string alias, string value)
    {
        var values = TestHelpers.ValidValues();
        values[alias] = value;

        var act = () => MeetRequestRecordMapper.Map(TestHelpers.Record(TestHelpers.Form(), values));

        act.Should().Throw<MeetRequestMappingException>().Which.Alias.Should().Be(alias);
    }

    [Fact]
    public void Start_time_in_a_dst_gap_is_rejected()
    {
        var values = TestHelpers.ValidValues();
        values["meetingDate"] = new DateTime(2026, 3, 29);   // clocks go forward 01:00→02:00 in London
        values["startTime"] = "01:30";

        var act = () => MeetRequestRecordMapper.Map(TestHelpers.Record(TestHelpers.Form(), values));

        act.Should().Throw<MeetRequestMappingException>().Which.Alias.Should().Be("startTime");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Description_is_optional(string? stored)
    {
        // Most requests are "organise an online/hybrid meetup" and need no elaboration, so the form only insists
        // on a title. An absent description must map to empty, not blow up.
        var values = TestHelpers.ValidValues();
        values["meetingDescription"] = stored;

        var request = MeetRequestRecordMapper.Map(TestHelpers.Record(TestHelpers.Form(), values));

        request.Description.Should().BeEmpty();
        request.Title.Should().Be("Umbraco London meetup");
    }


    [Fact]
    public void Success_writes_the_link_and_clears_the_error()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(TestHelpers.CompleteState()));

        record.GetRecordFieldByAlias("meetLink")!.ValuesAsString(false).Should().Be("https://meet.google.com/abc-defg-hij");
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().BeEmpty();
    }

    [Fact]
    public void The_state_json_is_never_written_to_the_record()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(TestHelpers.CompleteState()));

        // A Hidden field is nvarchar(255) and a real state object is ~600 characters; writing it there took the whole
        // approve request down. State belongs in IMeetStateStore.
        record.GetRecordFieldByAlias("provisioningState").Should().BeNull();
        record.RecordFields.Values.Should().OnlyContain(f => f.ValuesAsString(false).Length <= MeetRequestRecordMapper.MaxFormFieldLength);
    }

    [Fact]
    public void Manual_cohost_puts_the_approver_s_task_on_the_entry()
    {
        // The script's instructions used to reach the approver via provisioningState, which now lives in the state
        // store where nobody sees it. In manual mode the workflow still reports Completed, so if this field were
        // empty there would be nothing anywhere telling a human the Meet needs a co-host.
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());
        var state = TestHelpers.CompleteState();
        state.CohostAdded = false;
        state.CohostManual = true;
        state.CohostInstructions = "Open the event as the host account → Co-hosts → add someone@example.com → Save.";

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(state));

        record.GetRecordFieldByAlias("cohostAction")!.ValuesAsString(false)
            .Should().Be("Open the event as the host account → Co-hosts → add someone@example.com → Save.");
    }

    [Fact]
    public void Manual_cohost_without_instructions_still_says_something_actionable()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());
        var state = TestHelpers.CompleteState();
        state.CohostAdded = false;
        state.CohostManual = true;
        state.CohostInstructions = null;

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(state));

        record.GetRecordFieldByAlias("cohostAction")!.ValuesAsString(false).Should().Contain("co-host");
    }

    [Fact]
    public void An_automatically_added_cohost_leaves_no_task_behind()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());
        var state = TestHelpers.CompleteState();
        state.CohostAdded = true;
        state.CohostManual = false;

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(state));

        record.GetRecordFieldByAlias("cohostAction")!.ValuesAsString(false).Should().BeEmpty();
    }

    [Fact]
    public void Notes_and_transcript_are_off_unless_the_checkbox_is_ticked()
    {
        // Same principle as recording: transcribing a community meetup nobody asked to have written down is the
        // worse failure, and a form that predates the field must not silently switch it on.
        var form = TestHelpers.Form();
        var values = TestHelpers.ValidValues();
        values.Remove("transcribeMeeting");

        MeetRequestRecordMapper.Map(TestHelpers.Record(form, values)).TranscribeMeeting.Should().BeFalse();

        values["transcribeMeeting"] = false;
        MeetRequestRecordMapper.Map(TestHelpers.Record(form, values)).TranscribeMeeting.Should().BeFalse();

        values["transcribeMeeting"] = true;
        MeetRequestRecordMapper.Map(TestHelpers.Record(form, values)).TranscribeMeeting.Should().BeTrue();
    }

    [Fact]
    public void Notes_and_transcript_read_from_a_reloaded_records_string_value()
    {
        // A record reloaded from the database carries strings, not bools — the re-run-from-the-backoffice path.
        var form = TestHelpers.Form();
        var values = TestHelpers.ValidValues();
        values["transcribeMeeting"] = "true";

        MeetRequestRecordMapper.Map(TestHelpers.Record(form, values)).TranscribeMeeting.Should().BeTrue();
    }

    [Fact]
    public void A_deliberately_skipped_cohost_is_complete_and_leaves_no_task()
    {
        // COHOST_MODE=off: presenting is unrestricted, so no co-host is required. If this did not count as
        // complete, CreateMeetWorkflow would never short-circuit and every re-run would call Google again.
        var state = TestHelpers.CompleteState();
        state.CohostAdded = false;
        state.CohostManual = false;
        state.CohostSkipped = true;

        state.IsComplete.Should().BeTrue();

        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());
        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(state));

        record.GetRecordFieldByAlias("cohostAction")!.ValuesAsString(false).Should().BeEmpty();
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().BeEmpty();
    }

    [Fact]
    public void A_booking_with_no_cohost_outcome_at_all_is_not_complete()
    {
        // The guard the skipped flag exists for: neither added, nor handed over, nor skipped means the script
        // stopped before resolving the step, and a re-run should resume rather than short-circuit.
        var state = TestHelpers.CompleteState();
        state.CohostAdded = false;
        state.CohostManual = false;
        state.CohostSkipped = false;

        state.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Skipped_survives_a_state_round_trip()
    {
        var state = TestHelpers.CompleteState();
        state.CohostAdded = false;
        state.CohostSkipped = true;

        var restored = MeetProvisioningState.FromJson(state.ToJson())!;

        restored.CohostSkipped.Should().BeTrue();
        restored.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void A_degraded_cohost_shows_both_the_task_and_why_the_api_was_abandoned()
    {
        // COHOST_MODE=auto: the meeting is usable, so the script returns ok, but the approver has to finish the job
        // and we want the reason on the entry rather than only in the script's execution log.
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());
        var state = TestHelpers.CompleteState();
        state.CohostAdded = false;
        state.CohostManual = true;
        state.CohostInstructions = "Open the event as the host account → Co-hosts → add someone@example.com → Save.";
        state.CohostError = "POST v2beta/spaces/XYZ/members → HTTP 404: Method not found.";

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(state));

        record.GetRecordFieldByAlias("cohostAction")!.ValuesAsString(false).Should().Contain("Co-hosts");
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false)
            .Should().Be("cohost: POST v2beta/spaces/XYZ/members → HTTP 404: Method not found.");
    }

    [Fact]
    public void A_clean_booking_leaves_no_provisioning_error()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(TestHelpers.CompleteState()));

        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().BeEmpty();
    }

    [Fact]
    public void Oversized_values_are_truncated_rather_than_failing_the_write()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Failure("cohost", new string('x', 4000)));

        var written = record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false);
        written.Length.Should().Be(MeetRequestRecordMapper.MaxFormFieldLength);
        written.Should().EndWith("\u2026");
    }

    [Fact]
    public void Failure_writes_step_and_error()
    {
        var form = TestHelpers.Form();
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());
        var partial = new MeetProvisioningState { EventId = "evt1", MeetUri = "https://meet.google.com/abc-defg-hij" };

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Failure("cohost", "HTTP 403", partial));

        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().Be("cohost: HTTP 403");
        record.GetRecordFieldByAlias("meetLink")!.ValuesAsString(false).Should().Be("https://meet.google.com/abc-defg-hij", "a partially created Meet is still a Meet");
    }

    [Fact]
    public void WriteResult_creates_record_fields_that_exist_on_the_form_but_not_yet_on_the_record()
    {
        var form = TestHelpers.Form();
        var values = TestHelpers.ValidValues();      // no hidden-field values → no RecordFields for them
        var record = TestHelpers.Record(form, values);
        record.GetRecordFieldByAlias("meetLink").Should().BeNull();

        var created = MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(TestHelpers.CompleteState()));

        created.Select(f => f.Alias).Should().BeEquivalentTo(["meetLink", "provisioningError", "cohostAction"]);
        var field = record.GetRecordFieldByAlias("meetLink");
        field.Should().NotBeNull();
        field!.FieldId.Should().Be(form.AllFields.First(f => f.Alias == "meetLink").Id);
        field.Record.Should().Be(record.Id);
    }

    [Fact]
    public void WriteResult_reports_no_created_fields_when_they_already_exist()
    {
        var form = TestHelpers.Form();
        var values = TestHelpers.ValidValues();
        values["meetLink"] = ""; values["provisioningError"] = ""; values["cohostAction"] = "";
        var record = TestHelpers.Record(form, values);

        MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(TestHelpers.CompleteState())).Should().BeEmpty();
    }

    [Fact]
    public void WriteResult_throws_when_the_form_lacks_the_hidden_field()
    {
        var form = TestHelpers.Form(omitAliases: "meetLink");
        var record = TestHelpers.Record(form, TestHelpers.ValidValues());

        var act = () => MeetRequestRecordMapper.WriteResult(record, form, ProvisionResult.Success(TestHelpers.CompleteState()));

        act.Should().Throw<MeetRequestMappingException>().Which.Alias.Should().Be("meetLink");
    }

}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;
using UmbracoCommunity.MeetBooking.Forms;
using UmbracoCommunity.MeetBooking.Google;
using UmbracoCommunity.MeetBooking.Models;
using UmbracoCommunity.MeetBooking.Storage;
using UmbracoCommunity.MeetBooking.Workflows;
using Xunit;

namespace UmbracoCommunity.MeetBooking.Tests;

public class CreateMeetWorkflowTests
{
    private readonly Mock<IMeetProvisioner> _provisioner = new(MockBehavior.Strict);
    private readonly Mock<IRecordPersister> _persister = new();
    private readonly FakeMeetStateStore _stateStore = new();
    private readonly Form _form = TestHelpers.Form();

    private CreateMeetWorkflow Create(Action<MeetBookingOptions>? configure = null)
    {
        var options = TestHelpers.Options(configure);
        return new CreateMeetWorkflow(_provisioner.Object, _persister.Object, _stateStore, options,
            new TestHelpers.FixedTimeProvider(TestHelpers.Now), NullLogger<CreateMeetWorkflow>.Instance);
    }

    private WorkflowExecutionContext Context(Record record) => new(record, _form, FormState.Approved);

    private Record ValidRecord(Action<Dictionary<string, object?>>? tweak = null)
    {
        var values = TestHelpers.ValidValues();
        tweak?.Invoke(values);
        return TestHelpers.Record(_form, values);
    }

    [Fact]
    public void Identity_is_stable()
    {
        var sut = Create();
        sut.Id.Should().Be(CreateMeetWorkflow.WorkflowId);
        sut.Name.Should().Be("Community Meet: create Meet");
        sut.Group.Should().Be("Community Meet");
        sut.ValidateSettings().Should().BeEmpty();
    }

    [Fact]
    public async Task Happy_path_provisions_writes_back_and_completes()
    {
        var record = ValidRecord();
        _provisioner.Setup(p => p.ProvisionAsync(It.Is<MeetRequest>(r => r.RecordId == TestHelpers.RecordId && r.Title == "Umbraco London meetup"), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Success(TestHelpers.CompleteState()));

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        record.GetRecordFieldByAlias("meetLink")!.ValuesAsString(false).Should().Be("https://meet.google.com/abc-defg-hij");
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().BeEmpty();
        _persister.Verify(p => p.Save(record, _form, It.Is<IReadOnlyCollection<RecordField>>(c => c.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task Provisioner_failure_writes_partial_state_and_fails()
    {
        var record = ValidRecord();
        var partial = new MeetProvisioningState { EventId = "evt1", MeetUri = "https://meet.google.com/abc-defg-hij", SpaceName = "spaces/XYZ", Configured = true };
        _provisioner.Setup(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Failure("cohost", "HTTP 403", partial));

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().Be("cohost: HTTP 403");
        record.GetRecordFieldByAlias("meetLink")!.ValuesAsString(false).Should().Be("https://meet.google.com/abc-defg-hij");
        _stateStore.Read(TestHelpers.RecordId)!.EventId.Should().Be("evt1", "the partial state must survive so a re-run resumes instead of minting a second Meet");
        _stateStore.ErrorFor(TestHelpers.RecordId).Should().Be("cohost: HTTP 403");
        _persister.Verify(p => p.Save(record, _form, It.IsAny<IReadOnlyCollection<RecordField>>()), Times.Once);
    }

    [Fact]
    public async Task State_is_saved_even_when_the_record_write_back_fails()
    {
        // Regression: provisioning state used to be written to a Hidden form field, which Forms stores in
        // nvarchar(255). A real state object is ~600 characters, so the write threw, and because that SQL runs on
        // Umbraco's ambient scope it took the whole approve request down — leaving a real Google event that nothing
        // had a record of, so the next retry made a second one. State must now survive a failing record write.
        var record = ValidRecord();
        _provisioner.Setup(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Success(TestHelpers.CompleteState()));
        _persister.Setup(p => p.Save(It.IsAny<Record>(), It.IsAny<Form>(), It.IsAny<IReadOnlyCollection<RecordField>>()))
            .Throws(new InvalidOperationException("String or binary data would be truncated"));

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed, "a cosmetic write-back failure must not fail the workflow");
        _stateStore.Read(TestHelpers.RecordId)!.EventId.Should().Be("evt1");
    }

    [Fact]
    public async Task State_is_saved_before_the_record_write_back_is_attempted()
    {
        var record = ValidRecord();
        var order = new List<string>();
        _provisioner.Setup(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Success(TestHelpers.CompleteState()));
        _persister.Setup(p => p.Save(It.IsAny<Record>(), It.IsAny<Form>(), It.IsAny<IReadOnlyCollection<RecordField>>()))
            .Callback(() => order.Add("record"));
        _stateStore.OnWrite = () => order.Add("state");

        await Create().ExecuteAsync(Context(record));

        _stateStore.WriteCount.Should().Be(1);
        order.Should().Equal(["state", "record"]);
    }

    [Fact]
    public async Task Rerun_passes_previous_partial_state_to_the_provisioner()
    {
        var partial = new MeetProvisioningState { EventId = "evt1", SpaceName = "spaces/XYZ", Configured = true };
        var record = ValidRecord(v => { v["provisioningError"] = "cohost: HTTP 403"; v["meetLink"] = ""; });
        _stateStore.Seed(TestHelpers.RecordId, partial);
        _provisioner.Setup(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), It.Is<MeetProvisioningState>(s => s.EventId == "evt1" && s.Configured), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Success(TestHelpers.CompleteState()));

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().BeEmpty("the error clears on success");
        _provisioner.VerifyAll();
    }

    [Fact]
    public async Task Already_complete_state_short_circuits_without_calling_google()
    {
        var record = ValidRecord();
        _stateStore.Seed(TestHelpers.RecordId, TestHelpers.CompleteState());

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        _provisioner.VerifyNoOtherCalls();
        _persister.Verify(p => p.Save(It.IsAny<Record>(), It.IsAny<Form>(), It.IsAny<IReadOnlyCollection<RecordField>>()), Times.Never);
    }

    [Fact]
    public async Task Unmappable_record_fails_with_the_alias_in_the_error()
    {
        var record = ValidRecord(v => v["googleEmail"] = null);

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().Contain("googleEmail");
        _provisioner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Start_in_the_past_fails_before_calling_google()
    {
        var record = ValidRecord(v => v["meetingDate"] = new DateTime(2026, 9, 1));

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Failed);
        record.GetRecordFieldByAlias("provisioningError")!.ValuesAsString(false).Should().Contain("in the past");
        _provisioner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Past_start_is_allowed_when_resuming_an_existing_event()
    {
        var partial = new MeetProvisioningState { EventId = "evt1", SpaceName = "spaces/XYZ" };
        var record = ValidRecord(v => v["meetingDate"] = new DateTime(2026, 9, 1));
        _stateStore.Seed(TestHelpers.RecordId, partial);
        _provisioner.Setup(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), It.IsAny<MeetProvisioningState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Success(TestHelpers.CompleteState()));

        (await Create().ExecuteAsync(Context(record))).Should().Be(WorkflowExecutionStatus.Completed);
    }

    [Fact]
    public async Task DryRun_writes_a_placeholder_and_never_calls_google()
    {
        var record = ValidRecord();

        var status = await Create(o => o.DryRun = true).ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        record.GetRecordFieldByAlias("meetLink")!.ValuesAsString(false).Should().Be("https://meet.google.com/dry-run");
        _stateStore.Read(TestHelpers.RecordId)!.IsComplete
            .Should().BeFalse("a dry-run placeholder must not block the real run later");
        _provisioner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Real_run_after_a_dry_run_still_creates_the_meet()
    {
        var record = ValidRecord();
        await Create(o => o.DryRun = true).ExecuteAsync(Context(record));
        _provisioner.Setup(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), It.IsAny<MeetProvisioningState?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Success(TestHelpers.CompleteState()));

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        record.GetRecordFieldByAlias("meetLink")!.ValuesAsString(false).Should().Be("https://meet.google.com/abc-defg-hij");
        _provisioner.Verify(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), It.IsAny<MeetProvisioningState?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Disabled_completes_and_touches_nothing()
    {
        var record = ValidRecord();

        var status = await Create(o => o.Enabled = false).ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed);
        record.GetRecordFieldByAlias("meetLink").Should().BeNull();
        _provisioner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Persister_exception_does_not_hide_the_provisioning_outcome()
    {
        var record = ValidRecord();
        _provisioner.Setup(p => p.ProvisionAsync(It.IsAny<MeetRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProvisionResult.Success(TestHelpers.CompleteState()));
        _persister.Setup(p => p.Save(It.IsAny<Record>(), It.IsAny<Form>(), It.IsAny<IReadOnlyCollection<RecordField>>())).Throws(new InvalidOperationException("db down"));

        var status = await Create().ExecuteAsync(Context(record));

        status.Should().Be(WorkflowExecutionStatus.Completed, "the Meet exists; the write-back failure is logged, not surfaced as a provisioning failure");
        record.GetRecordFieldByAlias("meetLink")!.ValuesAsString(false).Should().Be("https://meet.google.com/abc-defg-hij", "in-memory record still carries the link for the next workflow");
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Enums;
using UmbracoCommunity.MeetBooking.Forms;
using UmbracoCommunity.MeetBooking.Google;
using UmbracoCommunity.MeetBooking.Models;
using UmbracoCommunity.MeetBooking.Storage;

namespace UmbracoCommunity.MeetBooking.Workflows;

/// <summary>
/// Umbraco Forms workflow for the community Meet request form. Attach it on the <b>Approve</b> stage: when HQ approves
/// an entry it asks the community Apps Script to create the Calendar event + Meet, then writes the Meet link and the
/// script's <c>state</c> back onto the entry's hidden fields.
/// </summary>
/// <remarks>
/// Failure returns <see cref="WorkflowExecutionStatus.Failed"/>, which Forms shows on the entry and lets you re-run.
/// The partial state is saved to <see cref="IMeetStateStore"/> before anything else, so the re-run resumes where it
/// stopped instead of creating a second Meet. The Apps Script guards the same thing from its side by tagging events
/// with the record id and adopting an existing one before inserting.
/// A subsequent <em>Slack</em> workflow on the same stage sees the written-back fields because the record is updated
/// in memory as well as persisted.
/// </remarks>
public sealed class CreateMeetWorkflow : WorkflowType
{
    public static readonly Guid WorkflowId = new("6f2c1b0a-7d0e-4c3a-9b3e-2e4c0a1d5f02");

    private readonly IMeetProvisioner _provisioner;
    private readonly IRecordPersister _persister;
    private readonly IMeetStateStore _stateStore;
    private readonly IOptionsMonitor<MeetBookingOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<CreateMeetWorkflow> _logger;

    public CreateMeetWorkflow(
        IMeetProvisioner provisioner,
        IRecordPersister persister,
        IMeetStateStore stateStore,
        IOptionsMonitor<MeetBookingOptions> options,
        TimeProvider clock,
        ILogger<CreateMeetWorkflow> logger)
    {
        _provisioner = provisioner;
        _persister = persister;
        _stateStore = stateStore;
        _options = options;
        _clock = clock;
        _logger = logger;

        Id = WorkflowId;
        Name = "Community Meet: create Meet";
        Description = "Creates the Google Calendar event + Meet for this request via the community Apps Script and writes the link back onto the entry. Attach on the Approve stage, before any Slack workflow.";
        Icon = "icon-video";
        Group = "Community Meet";
    }

    public override async Task<WorkflowExecutionStatus> ExecuteAsync(WorkflowExecutionContext context)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _logger.LogInformation("MeetBooking is disabled; skipping record {RecordId}", context.Record.UniqueId);
            return WorkflowExecutionStatus.Completed;
        }

        MeetRequest request;
        try
        {
            request = MeetRequestRecordMapper.Map(context.Record);
        }
        catch (MeetRequestMappingException ex)
        {
            _logger.LogError(ex, "Record {RecordId} could not be mapped to a Meet request", context.Record.UniqueId);
            Record(context, ProvisionResult.Failure("form", ex.Message, _stateStore.Read(context.Record.UniqueId)));
            return WorkflowExecutionStatus.Failed;
        }

        var previous = _stateStore.Read(request.RecordId);
        if (previous?.IsComplete == true)
        {
            _logger.LogInformation("Record {RecordId} already has a complete Meet ({MeetUri}); nothing to do", request.RecordId, previous.MeetUri);
            return WorkflowExecutionStatus.Completed;
        }

        if (previous?.EventId is null && IsInThePast(request))
        {
            Record(context, ProvisionResult.Failure("form", $"start time {request.StartLocal:yyyy-MM-dd HH:mm} {request.TimeZone} is in the past"));
            return WorkflowExecutionStatus.Failed;
        }

        if (opts.DryRun)
        {
            _logger.LogInformation("DryRun: would create Meet for record {RecordId} ({Title}, {Start} {TimeZone}, {Duration} min, record={Record})",
                request.RecordId, request.Title, request.StartLocal, request.TimeZone, request.DurationMinutes, request.RecordMeeting);
            // Deliberately *incomplete* (no EventId): once DryRun is switched off, re-running the workflow on this entry
            // must still create the real Meet rather than short-circuit on a placeholder.
            Record(context, ProvisionResult.Success(new MeetProvisioningState
            {
                MeetUri = "https://meet.google.com/dry-run", CohostManual = true, CohostInstructions = "DryRun — nothing was created.",
            }));
            return WorkflowExecutionStatus.Completed;
        }

        var result = await _provisioner.ProvisionAsync(request, previous, CancellationToken.None);
        Record(context, result);

        if (result.Ok)
        {
            _logger.LogInformation("Created Meet {MeetUri} for record {RecordId}", result.State.MeetUri, request.RecordId);
            return WorkflowExecutionStatus.Completed;
        }

        _logger.LogError("Meet provisioning failed for record {RecordId} at step {Step}: {Error}", request.RecordId, result.Step, result.Error);
        return WorkflowExecutionStatus.Failed;
    }

    public override List<Exception> ValidateSettings() => [];

    private bool IsInThePast(MeetRequest request)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZone, out var tz)) return false;
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(request.StartLocal, tz);
        return startUtc < _clock.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Records the outcome twice over: the machine-readable state in our own table, then the human-facing link and
    /// error on the entry itself.
    /// </summary>
    /// <remarks>
    /// Order matters. The state store goes first and on its own connection, because it is what stops a retry from
    /// minting a second Meet. The entry write-back is the cosmetic half — it is what the approver and the following
    /// Slack workflow read — so a failure there is logged and swallowed rather than allowed to fail the workflow.
    /// </remarks>
    private void Record(WorkflowExecutionContext context, ProvisionResult result)
    {
        _stateStore.Write(context.Record.UniqueId, result.State, result.Ok ? null : $"{result.Step}: {result.Error}");

        try
        {
            var created = MeetRequestRecordMapper.WriteResult(context.Record, context.Form, result);
            _persister.Save(context.Record, context.Form, created);
        }
        catch (Exception ex)
        {
            // The Meet may well exist by now, and the state store above already knows about it, so this is not fatal.
            _logger.LogError(ex, "Could not write the Meet link onto record {RecordId}; the provisioning state was saved regardless", context.Record.UniqueId);
        }
    }
}

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Attributes;
using Umbraco.Forms.Core.Enums;
using UmbracoCommunity.MeetBooking.Models;
using UmbracoCommunity.MeetBooking.Storage;

namespace UmbracoCommunity.MeetBooking.Workflows;

/// <summary>
/// Posts one short Slack line when a Meet request is approved, in place of Forms' built-in Slack workflow.
/// </summary>
/// <remarks>
/// Forms' own Slack workflow type takes only a webhook URL and always dumps every field on the entry, which for this
/// form is a dozen lines the approver has to read past to find the two things that matter: the Meet link, and whether
/// they still owe us a co-host. This posts exactly those.
/// <para>
/// Attach it on the <b>Approve</b> stage, <em>after</em> <see cref="CreateMeetWorkflow"/> — it reads the state that
/// workflow wrote. A failed booking still gets a message, because a silent failure is worse than a noisy one.
/// </para>
/// </remarks>
public sealed class PostMeetToSlackWorkflow : WorkflowType
{
    public static readonly Guid WorkflowId = new("3c9b6e41-59a7-4d0e-9c2f-8b1a4f7d6e05");

    /// <summary>Slack's own cap is 40 000 characters; nothing here comes close, but a runaway title shouldn't post a wall.</summary>
    internal const int MaxMessageLength = 3000;

    private readonly IMeetStateStore _stateStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PostMeetToSlackWorkflow> _logger;

    /// <remarks>
    /// No <c>View</c> is specified on purpose. Forms 18's backoffice resolves a setting's editor by property-editor
    /// UI alias (<c>Umb.PropertyEditorUi.*</c>) and falls back to a plain text input when none is given — the old
    /// <c>View = "TextField"</c> convention names a view that no longer exists, and renders as
    /// "The configured property editor UI could not be found". Forms' own Slack workflow omits it the same way.
    /// </remarks>
    [Setting("Webhook URL", Description = "Slack incoming-webhook URL. The channel is fixed by the webhook itself.")]
    public string WebhookUrl { get; set; } = string.Empty;

    public PostMeetToSlackWorkflow(
        IMeetStateStore stateStore,
        IHttpClientFactory httpClientFactory,
        ILogger<PostMeetToSlackWorkflow> logger)
    {
        _stateStore = stateStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        Id = WorkflowId;
        Name = "Community Meet: post to Slack";
        Description = "Posts a one-line Slack message with the Meet link and the outstanding co-host task. Attach on the Approve stage, after Create Meet.";
        Icon = "icon-message";
        Group = "Community Meet";
    }

    public override async Task<WorkflowExecutionStatus> ExecuteAsync(WorkflowExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(WebhookUrl))
        {
            _logger.LogError("Slack webhook URL is not configured on the workflow; nothing posted for record {RecordId}", context.Record.UniqueId);
            return WorkflowExecutionStatus.Failed;
        }

        var state = _stateStore.Read(context.Record.UniqueId);
        var message = BuildMessage(state, TitleOf(context.Record), RequesterNameOf(context.Record), CohostEmailOf(context.Record));

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new StringContent(
                JsonSerializer.Serialize(new { text = message }), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(WebhookUrl, content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Slack answers a bad webhook with a plain-text reason ("no_service", "invalid_payload") — log it,
                // because the status code alone never says which.
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Slack rejected the post for record {RecordId}: HTTP {Status} {Body}",
                    context.Record.UniqueId, (int)response.StatusCode, body);
                return WorkflowExecutionStatus.Failed;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Could not post to Slack for record {RecordId}", context.Record.UniqueId);
            return WorkflowExecutionStatus.Failed;
        }

        return WorkflowExecutionStatus.Completed;
    }

    public override List<Exception> ValidateSettings() =>
        string.IsNullOrWhiteSpace(WebhookUrl)
            ? [new Exception("A Slack webhook URL is required.")]
            : [];

    internal const string HttpClientName = "MeetBookingSlack";

    /// <summary>
    /// The message, as Slack mrkdwn. Kept pure so the wording is testable without a webhook.
    /// </summary>
    /// <remarks>
    /// Deliberately does <b>not</b> link the Meet: the meeting is usually weeks away, and a Meet link in a channel
    /// is something people click. The link goes to the calendar event instead, which is the thing anyone reading
    /// this might actually want to open — to check the time, or adjust the co-hosts.
    /// <para>
    /// When a co-host still has to be added by hand, the sentence is the Apps Script's own
    /// <c>cohostInstructions</c> verbatim, so that wording lives in one place and adapts by itself to the
    /// configured access and presenting rules.
    /// </para>
    /// </remarks>
    internal static string BuildMessage(MeetProvisioningState? state, string title, string? requesterName, string? cohostEmail)
    {
        // "Owain Jones (Community hour)", or just the title when no name was given.
        var subject = string.IsNullOrWhiteSpace(requesterName)
            ? Escape(title)
            : $"{Escape(requesterName.Trim())} ({Escape(title)})";

        if (state?.MeetUri is null or "")
        {
            // No Meet means the booking failed or Create Meet never ran. Say so rather than posting nothing.
            return $"⚠️ Community meeting *not* created for {subject} — check the entry in the backoffice.";
        }

        // Only the word "created" carries the link, so the line reads as a sentence rather than a banner.
        var created = string.IsNullOrWhiteSpace(state.HtmlLink) ? "created" : Link(state.HtmlLink!, "created");
        var builder = new StringBuilder($"✅ Community meeting {created} for {subject}.").Append('\n');

        var who = string.IsNullOrWhiteSpace(cohostEmail) ? "The requester" : Escape(cohostEmail.Trim());

        if (state.CohostAdded)
        {
            builder.Append($"{who} has been invited and made a co-host, so they can start the Meet on their own.");
        }
        else if (state.CohostManual && !string.IsNullOrWhiteSpace(state.CohostInstructions))
        {
            // They are invited either way — the invite goes out with the event — but not yet able to host.
            builder.Append($"{who} has been invited, but is *not* a co-host yet. ")
                   .Append(LinkifyEventPrefix(Escape(state.CohostInstructions.Trim()), state.HtmlLink));
        }
        else
        {
            builder.Append($"{who} has been invited.");
        }

        var message = builder.ToString();
        return message.Length <= MaxMessageLength ? message : message[..(MaxMessageLength - 1)] + "…";
    }

    /// <summary>Puts the calendar event behind the instruction's "Open the event" opening, when it starts that way.</summary>
    private static string LinkifyEventPrefix(string instruction, string? htmlLink)
    {
        const string prefix = "Open the event";
        return string.IsNullOrWhiteSpace(htmlLink) || !instruction.StartsWith(prefix, StringComparison.Ordinal)
            ? instruction
            : Link(htmlLink, prefix) + instruction[prefix.Length..];
    }

    private static string Link(string url, string label) => $"<{url}|{label}>";

    /// <summary>
    /// Slack requires exactly these three escaped in message text. Applied to anything a requester typed, so a title
    /// containing &lt; or | cannot break out of a link or forge one.
    /// </summary>
    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("|", "&#124;", StringComparison.Ordinal);

    private static string TitleOf(Umbraco.Forms.Core.Persistence.Dtos.Record record) =>
        FieldOf(record, Forms.MeetRequestFieldAliases.Title) ?? "this request";

    private static string? CohostEmailOf(Umbraco.Forms.Core.Persistence.Dtos.Record record) =>
        FieldOf(record, Forms.MeetRequestFieldAliases.CohostEmail);

    private static string? RequesterNameOf(Umbraco.Forms.Core.Persistence.Dtos.Record record) =>
        FieldOf(record, Forms.MeetRequestFieldAliases.RequesterName);

    private static string? FieldOf(Umbraco.Forms.Core.Persistence.Dtos.Record record, string alias)
    {
        var value = record.GetRecordFieldByAlias(alias)?.ValuesAsString(false);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

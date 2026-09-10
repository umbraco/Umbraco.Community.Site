using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.MeetBooking.Models;

namespace UmbracoCommunity.MeetBooking.Google;

/// <summary>
/// One HTTPS POST to the community Meet booking Apps Script (see <c>tools/meet-booking-apps-script/</c>). The script
/// does the Calendar and Meet work as the host account; we only ship the request and read back <c>ok</c>, <c>step</c>,
/// <c>error</c> and <c>state</c>. Apps Script always answers 200 and serves the body after a 302, which
/// <see cref="HttpClient"/> follows by default — so success is decided by the body, never the status code.
/// </summary>
/// <summary>Typed-client marker so the Apps Script <see cref="HttpClient"/> gets its own timeout and User-Agent.</summary>
public sealed class AppsScriptHttpClient(HttpClient httpClient)
{
    public HttpClient HttpClient { get; } = httpClient;
}

/// <summary>
/// Creates (or resumes creating) the Calendar event + Meet for a request. Never throws for a bad response — that is a
/// <see cref="ProvisionResult"/> with <c>Ok = false</c>.
/// </summary>
public interface IMeetProvisioner
{
    Task<ProvisionResult> ProvisionAsync(MeetRequest request, MeetProvisioningState? previousState, CancellationToken cancellationToken);
}

public sealed class AppsScriptMeetProvisioner(
    AppsScriptHttpClient client,
    IOptionsMonitor<MeetBookingOptions> options,
    ILogger<AppsScriptMeetProvisioner> logger) : IMeetProvisioner
{
    internal const string TransportStep = "transport";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<ProvisionResult> ProvisionAsync(MeetRequest request, MeetProvisioningState? previousState, CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue.AppsScript;
        if (string.IsNullOrWhiteSpace(opts.WebAppUrl) || string.IsNullOrWhiteSpace(opts.SharedSecret))
            return ProvisionResult.Failure(TransportStep, "MeetBooking:AppsScript:WebAppUrl / SharedSecret are not configured", previousState);
        if (!Uri.TryCreate(opts.WebAppUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            return ProvisionResult.Failure(TransportStep, "MeetBooking:AppsScript:WebAppUrl must be an absolute https URL (the shared secret travels in the body)", previousState);

        var body = BuildBody(request, previousState, opts.SharedSecret);

        HttpResponseMessage response;
        string text;
        try
        {
            response = await client.HttpClient.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
            text = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Apps Script call failed for record {RecordId}", request.RecordId);
            return ProvisionResult.Failure(TransportStep, ex.Message, previousState);
        }

        return ParseResponse(text, (int)response.StatusCode, previousState, request.RecordId, logger);
    }

    internal static AppsScriptRequest BuildBody(MeetRequest request, MeetProvisioningState? previousState, string secret) => new(
        Token: secret,
        RecordId: request.RecordId.ToString("N"),
        Title: request.Title,
        Description: BuildDescription(request),
        RequesterName: request.RequesterName,
        CohostEmail: request.CohostEmail,
        StartLocal: request.StartLocal.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        DurationMinutes: request.DurationMinutes,
        TimeZone: request.TimeZone,
        RecordMeeting: request.RecordMeeting,
        TranscribeMeeting: request.TranscribeMeeting,
        State: previousState);

    /// <summary>What the requester wrote; the script appends its own "how to run this Meet" cheat-sheet.</summary>
    private static string BuildDescription(MeetRequest request) => request.Description.Trim();

    internal static ProvisionResult ParseResponse(string text, int statusCode, MeetProvisioningState? previousState, Guid recordId, ILogger logger)
    {
        AppsScriptResponse? parsed = null;
        if (text.TrimStart().StartsWith('{'))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<AppsScriptResponse>(text, JsonOptions);
            }
            catch (JsonException)
            {
                // fall through to the transport error below
            }
        }

        if (parsed is null)
        {
            // Not JSON: typically Google's sign-in page (deployment access isn't "Anyone") or an HTML error page.
            var snippet = text.Length > 300 ? text[..300] : text;
            logger.LogError("Apps Script returned a non-JSON response (HTTP {Status}) for record {RecordId}: {Snippet}", statusCode, recordId, snippet);
            return ProvisionResult.Failure(TransportStep, $"HTTP {statusCode}, non-JSON response: {snippet}", previousState);
        }

        var state = parsed.State ?? previousState ?? new MeetProvisioningState();
        if (parsed.Ok)
        {
            if (state.IsComplete && !string.IsNullOrEmpty(state.MeetUri)) return ProvisionResult.Success(state);

            // "ok" without a usable state is a contract violation (script edited, response truncated) — never report success.
            logger.LogError("Apps Script said ok but returned an incomplete state for record {RecordId}: {State}", recordId, state.ToJson());
            return ProvisionResult.Failure("contract", "script reported success but the returned state is incomplete", state);
        }

        logger.LogWarning("Apps Script reported failure at step {Step} for record {RecordId}: {Error}", parsed.Step, recordId, parsed.Error);
        return ProvisionResult.Failure(parsed.Step ?? "unknown", parsed.Error ?? "unknown error", state);
    }

    internal sealed record AppsScriptRequest(
        string Token,
        string RecordId,
        string Title,
        string Description,
        string RequesterName,
        string CohostEmail,
        string StartLocal,
        int DurationMinutes,
        string TimeZone,
        bool RecordMeeting,
        bool TranscribeMeeting,
        MeetProvisioningState? State);

    internal sealed class AppsScriptResponse
    {
        public bool Ok { get; set; }
        public string? Step { get; set; }
        public string? Error { get; set; }
        public MeetProvisioningState? State { get; set; }
    }
}

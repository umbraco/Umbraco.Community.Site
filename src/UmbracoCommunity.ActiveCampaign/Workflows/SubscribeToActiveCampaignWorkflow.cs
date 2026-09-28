using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Attributes;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;

namespace UmbracoCommunity.ActiveCampaign.Workflows;

/// <summary>
/// Subscribes the submitter of a form to an ActiveCampaign list: upserts the contact by email
/// (<c>POST contact/sync</c>), then adds it to the configured list as active (<c>POST contactLists</c>, <c>status: 1</c>),
/// then optionally adds the configured tags to it (<c>POST contactTags</c>, one call per tag).
/// </summary>
/// <remarks>
/// Forms' built-in "Send form to URL" workflow can't do this: ActiveCampaign wants an <c>Api-Token</c> header and a
/// JSON body, and the second call needs the contact id from the first.
/// <para>
/// <b><c>status: 1</c> subscribes immediately and bypasses ActiveCampaign's double opt-in.</b> Attach this only to a
/// form with an explicit, unticked-by-default consent checkbox, and put that checkbox's alias in
/// <see cref="ConsentFieldAlias"/>.
/// </para>
/// <para>
/// The sync and list calls are idempotent (sync upserts by email; re-adding an existing list membership just re-asserts
/// the status), so re-running the workflow from the backoffice after a failure is safe. What ActiveCampaign does with a
/// tag the contact already has isn't verified here; whatever it answers, a failed tag call is only a warning (see
/// <see cref="TagIds"/>), so a re-run still completes.
/// </para>
/// </remarks>
public sealed class SubscribeToActiveCampaignWorkflow : WorkflowType
{
    public static readonly Guid WorkflowId = new("e93cf240-8336-4561-aab0-6b6ceda07775");

    internal const string HttpClientName = "ActiveCampaign";
    internal const string DefaultEmailFieldAlias = "email";
    internal const string ContactSyncPath = "contact/sync";
    internal const string ContactListsPath = "contactLists";
    internal const string ContactTagsPath = "contactTags";

    /// <summary>Error bodies are logged for diagnosis; this stops a proxy's HTML error page flooding the log.</summary>
    internal const int MaxLoggedBodyLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ActiveCampaignOptions> _options;
    private readonly ILogger<SubscribeToActiveCampaignWorkflow> _logger;

    // No `View` on any setting: Forms 18 falls back to a plain text input, which is what these are. See the remark on
    // PostMeetToSlackWorkflow.WebhookUrl for why the old `View = "TextField"` convention must not be used.

    [Setting("List ID", Description = "The numeric ID of the ActiveCampaign list to subscribe to (Lists → open the list → the number in the URL).", IsMandatory = true, DisplayOrder = 10)]
    public string ListId { get; set; } = string.Empty;

    /// <remarks>
    /// Forms loads a setting that was never saved as <c>string.Empty</c>, overwriting this initialiser, so the default
    /// is re-applied at execution time too (<see cref="ResolvedEmailFieldAlias"/>). The initialiser still matters: Forms
    /// uses it as the default value shown in the backoffice.
    /// </remarks>
    [Setting("Email field alias", Description = "Alias of the form field holding the subscriber's email address. Defaults to 'email'.", DisplayOrder = 20)]
    public string EmailFieldAlias { get; set; } = DefaultEmailFieldAlias;

    [Setting("First name field alias", Description = "Optional. Alias of the field holding the subscriber's first name.", DisplayOrder = 30)]
    public string FirstNameFieldAlias { get; set; } = string.Empty;

    [Setting("Last name field alias", Description = "Optional. Alias of the field holding the subscriber's last name.", DisplayOrder = 40)]
    public string LastNameFieldAlias { get; set; } = string.Empty;

    [Setting("Consent field alias", Description = "Alias of the consent checkbox. When set, the submitter is only subscribed if it is ticked. Strongly recommended: this workflow subscribes immediately, bypassing ActiveCampaign's double opt-in.", DisplayOrder = 50)]
    public string ConsentFieldAlias { get; set; } = string.Empty;

    /// <remarks>
    /// Tagging runs only after the contact is on the list, and a tag that can't be added (non-2xx, timeout, network
    /// error) is logged as a warning without failing the entry: the subscription itself has already succeeded, and
    /// failing would invite a re-run for something that isn't wrong.
    /// </remarks>
    [Setting("Tag IDs", Description = "Optional. Comma-separated numeric IDs of ActiveCampaign tags to add to the contact once subscribed, e.g. '12, 34'. A tag that can't be added is logged but doesn't fail the entry.", DisplayOrder = 60)]
    public string TagIds { get; set; } = string.Empty;

    public SubscribeToActiveCampaignWorkflow(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ActiveCampaignOptions> options,
        ILogger<SubscribeToActiveCampaignWorkflow> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;

        Id = WorkflowId;
        Name = "Subscribe to ActiveCampaign";
        Description = "Adds the submitter to an ActiveCampaign list (subscribed immediately, no double opt-in). Set the consent field alias so only people who ticked the box are subscribed.";
        Icon = "icon-message";
        Group = "Services";
    }

    internal string ResolvedEmailFieldAlias =>
        string.IsNullOrWhiteSpace(EmailFieldAlias) ? DefaultEmailFieldAlias : EmailFieldAlias.Trim();

    public override async Task<WorkflowExecutionStatus> ExecuteAsync(WorkflowExecutionContext context)
    {
        var recordId = context.Record.UniqueId;
        var opts = _options.CurrentValue;

        if (!opts.IsConfigured || !opts.TryGetBaseUri(out var baseUri))
        {
            _logger.LogError("ActiveCampaign is not configured (ActiveCampaign:BaseUrl / ActiveCampaign:ApiKey); record {RecordId} was not subscribed", recordId);
            return WorkflowExecutionStatus.Failed;
        }

        if (!TryParsePositiveId(ListId, out var listId))
        {
            // ValidateSettings stops this being saved from the backoffice, but a form imported or edited elsewhere can still carry it.
            _logger.LogError("ActiveCampaign workflow on form {FormName} has no valid List ID; record {RecordId} was not subscribed", context.Form.Name, recordId);
            return WorkflowExecutionStatus.Failed;
        }

        if (!TryParseTagIds(TagIds, out var tagIds, out _))
        {
            // As with the List ID: ValidateSettings stops this being saved, but an imported form can still carry it.
            _logger.LogError("ActiveCampaign workflow on form {FormName} has invalid Tag IDs; record {RecordId} was not subscribed", context.Form.Name, recordId);
            return WorkflowExecutionStatus.Failed;
        }

        if (!string.IsNullOrWhiteSpace(ConsentFieldAlias))
        {
            var consentAlias = ConsentFieldAlias.Trim();
            if (!FormHasField(context.Form, consentAlias))
            {
                // A typo here would otherwise read as "nobody ever consents" and silently subscribe no-one.
                _logger.LogWarning("Consent field alias {ConsentFieldAlias} is not a field on form {FormName}; record {RecordId} was not subscribed", consentAlias, context.Form.Name, recordId);
                return WorkflowExecutionStatus.Failed;
            }

            if (!IsTicked(context.Record, consentAlias))
            {
                // Not an error: the submitter declined. Completed keeps the entry out of the failed-workflow view.
                _logger.LogInformation("Consent field {ConsentFieldAlias} not ticked on record {RecordId}; not subscribing to ActiveCampaign", consentAlias, recordId);
                return WorkflowExecutionStatus.Completed;
            }
        }

        var emailAlias = ResolvedEmailFieldAlias;
        var email = ReadString(context.Record, emailAlias);
        if (email is null)
        {
            // The email is personal data, so it is never logged — the record id is enough to find the entry.
            _logger.LogWarning("Email field {EmailFieldAlias} is empty or missing on record {RecordId} (if the field is marked sensitive, untick the workflow's 'exclude sensitive data'); not subscribing to ActiveCampaign", emailAlias, recordId);
            return WorkflowExecutionStatus.Failed;
        }

        if (!IsValidEmail(email))
        {
            _logger.LogWarning("Email field {EmailFieldAlias} on record {RecordId} is not a valid email address; not subscribing to ActiveCampaign", emailAlias, recordId);
            return WorkflowExecutionStatus.Failed;
        }

        var contact = new ContactPayload(
            email,
            OptionalString(context.Record, FirstNameFieldAlias),
            OptionalString(context.Record, LastNameFieldAlias));

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);

            using var sync = await PostAsync(client, new Uri(baseUri, ContactSyncPath), new { contact }, opts.ApiKey, recordId);
            if (sync is null) return WorkflowExecutionStatus.Failed;

            if (!TryReadContactId(sync, out var contactId))
            {
                _logger.LogError("ActiveCampaign {Endpoint} response for record {RecordId} had no contact.id: {Body}",
                    ContactSyncPath, recordId, Redact(sync, opts.ApiKey));
                return WorkflowExecutionStatus.Failed;
            }

            var contactList = new { contactList = new { list = listId, contact = contactId, status = 1 } };
            using var added = await PostAsync(client, new Uri(baseUri, ContactListsPath), contactList, opts.ApiKey, recordId);
            if (added is null) return WorkflowExecutionStatus.Failed;

            _logger.LogInformation("Subscribed record {RecordId} to ActiveCampaign list {ListId} as contact {ContactId}", recordId, listId, contactId);

            await AddTagsAsync(client, new Uri(baseUri, ContactTagsPath), contactId, tagIds, opts.ApiKey, recordId);
            return WorkflowExecutionStatus.Completed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // TaskCanceledException is the HttpClient timeout. Neither exception type carries request headers, so the
            // API key cannot reach the log through here.
            context.Exception = ex;
            _logger.LogError(ex, "Could not subscribe record {RecordId} to ActiveCampaign list {ListId}", recordId, listId);
            return WorkflowExecutionStatus.Failed;
        }
    }

    public override List<Exception> ValidateSettings()
    {
        var errors = new List<Exception>();

        if (!TryParsePositiveId(ListId, out _))
            errors.Add(new Exception("An ActiveCampaign List ID (a positive whole number) is required."));

        if (!TryParseTagIds(TagIds, out _, out var invalidTagIds))
            errors.Add(new Exception($"ActiveCampaign Tag IDs must be positive whole numbers separated by commas; not valid: {string.Join(", ", invalidTagIds.Select(v => $"'{v}'"))}."));

        return errors;
    }

    /// <summary>
    /// Shown in the backoffice, which disables the workflow type in the picker while this returns anything.
    /// Workflows already attached still run — and fail cleanly — so a missing key is visible either way.
    /// </summary>
    public override IEnumerable<string> GetConfigurationErrors() =>
        _options.CurrentValue.IsConfigured
            ? []
            : ["ActiveCampaign:BaseUrl and ActiveCampaign:ApiKey must be set in configuration (see the UmbracoCommunity.ActiveCampaign README)."];

    /// <summary>
    /// POSTs <paramref name="payload"/> as JSON and returns the parsed response body, or <c>null</c> after logging a
    /// non-success response. Throws <see cref="HttpRequestException"/>, <see cref="TaskCanceledException"/> or
    /// <see cref="JsonException"/>, which the caller turns into <see cref="WorkflowExecutionStatus.Failed"/>.
    /// </summary>
    private async Task<JsonDocument?> PostAsync(HttpClient client, Uri uri, object payload, string apiKey, Guid recordId)
    {
        using var request = CreateRequest(uri, payload, apiKey);
        if (request is null)
        {
            _logger.LogError("ActiveCampaign:ApiKey is not a valid header value; record {RecordId} was not subscribed", recordId);
            return null;
        }

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ActiveCampaign {Endpoint} returned HTTP {Status} for record {RecordId}: {Body}",
                uri.AbsolutePath, (int)response.StatusCode, recordId, Redact(body, apiKey));
            return null;
        }

        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Adds each tag in turn (<c>POST contactTags</c>). Every failure is logged as a warning and the remaining tags are
    /// still tried; nothing here fails the workflow, because the contact is already subscribed by the time it runs.
    /// </summary>
    private async Task AddTagsAsync(HttpClient client, Uri uri, long contactId, IReadOnlyList<long> tagIds, string apiKey, Guid recordId)
    {
        if (tagIds.Count == 0) return;

        var tagged = 0;
        foreach (var tagId in tagIds)
        {
            try
            {
                // Numbers, like list and contact in the contactLists body.
                var contactTag = new { contactTag = new { contact = contactId, tag = tagId } };
                using var request = CreateRequest(uri, contactTag, apiKey);
                if (request is null)
                {
                    // Unreachable in practice: the same key was already accepted for the sync and list calls.
                    _logger.LogWarning("ActiveCampaign:ApiKey is not a valid header value; tag {TagId} was not added for record {RecordId}", tagId, recordId);
                    continue;
                }

                using var response = await client.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogWarning("ActiveCampaign {Endpoint} returned HTTP {Status} adding tag {TagId} for record {RecordId} (the contact is subscribed, just not tagged): {Body}",
                        uri.AbsolutePath, (int)response.StatusCode, tagId, recordId, Redact(body, apiKey));
                    continue;
                }

                tagged++;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // As in ExecuteAsync: neither exception type carries request headers, so the key can't leak through here.
                _logger.LogWarning(ex, "Could not add ActiveCampaign tag {TagId} for record {RecordId} (the contact is subscribed, just not tagged)", tagId, recordId);
            }
        }

        _logger.LogInformation("Added {TaggedCount} of {TagCount} ActiveCampaign tags to contact {ContactId} for record {RecordId}", tagged, tagIds.Count, contactId, recordId);
    }

    /// <summary>
    /// A JSON POST carrying the <c>Api-Token</c> header, or <c>null</c> if the key isn't a valid header value.
    /// </summary>
    private static HttpRequestMessage? CreateRequest(Uri uri, object payload, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };

        // TryAdd, not Add: Add throws a FormatException whose message quotes the offending value — i.e. the key.
        if (!request.Headers.TryAddWithoutValidation("Api-Token", apiKey.Trim()))
        {
            request.Dispose();
            return null;
        }

        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    /// <summary>
    /// ActiveCampaign returns ids as JSON strings (<c>"id": "113"</c>); a number is accepted too. Parsed to
    /// <see cref="long"/> so the contactLists body sends the same numeric type as <c>list</c>.
    /// </summary>
    private static bool TryReadContactId(JsonDocument sync, out long contactId)
    {
        contactId = 0;
        if (sync.RootElement.ValueKind != JsonValueKind.Object
            || !sync.RootElement.TryGetProperty("contact", out var contact)
            || contact.ValueKind != JsonValueKind.Object
            || !contact.TryGetProperty("id", out var id))
            return false;

        return id.ValueKind switch
        {
            JsonValueKind.Number => id.TryGetInt64(out contactId) && contactId > 0,
            JsonValueKind.String => long.TryParse(id.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out contactId) && contactId > 0,
            _ => false,
        };
    }

    private static bool TryParsePositiveId(string? raw, out long id) =>
        long.TryParse(raw?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    /// <summary>
    /// Parses the comma-separated <see cref="TagIds"/> setting: whitespace and empty entries (a trailing comma) are
    /// ignored, duplicates dropped with the first occurrence's order kept. Blank means no tags. Anything that isn't a
    /// positive whole number is returned in <paramref name="invalid"/>.
    /// </summary>
    internal static bool TryParseTagIds(string? raw, out IReadOnlyList<long> tagIds, out IReadOnlyList<string> invalid)
    {
        var ids = new List<long>();
        var bad = new List<string>();

        foreach (var entry in (raw ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParsePositiveId(entry, out var id))
            {
                if (!ids.Contains(id)) ids.Add(id);
            }
            else
            {
                bad.Add(entry);
            }
        }

        tagIds = ids;
        invalid = bad;
        return bad.Count == 0;
    }

    private static bool FormHasField(Form form, string alias) =>
        form.AllFields.Any(f => string.Equals(f.Alias, alias, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A Forms checkbox arrives as a <see cref="bool"/> at submit time and as a string (<c>"True"</c>) on a record
    /// reloaded for a backoffice re-run; an unticked plain checkbox may have no value at all. Anything other than a
    /// recognised "yes" counts as not ticked.
    /// </summary>
    internal static bool IsTicked(Record record, string alias) =>
        FirstValue(record, alias) switch
        {
            null => false,
            bool b => b,
            var v => Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant() is "true" or "on" or "1" or "yes",
        };

    /// <summary>
    /// Rejects display-name forms ("Jane &lt;jane@x.com&gt;") and anything <see cref="MailAddress"/> would reinterpret,
    /// by requiring the parsed address to equal the input exactly.
    /// </summary>
    internal static bool IsValidEmail(string value) =>
        !value.Any(char.IsWhiteSpace)
        && MailAddress.TryCreate(value, out var parsed)
        && string.Equals(parsed.Address, value, StringComparison.Ordinal)
        && parsed.Host.Contains('.');

    private static string? OptionalString(Record record, string? alias) =>
        string.IsNullOrWhiteSpace(alias) ? null : ReadString(record, alias.Trim());

    private static string? ReadString(Record record, string alias)
    {
        var value = FirstValue(record, alias);
        var text = value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static object? FirstValue(Record record, string alias)
    {
        var value = record.GetRecordFieldByAlias(alias)?.Values?.FirstOrDefault();
        return value is string s && string.IsNullOrWhiteSpace(s) ? null : value;
    }

    private static string Redact(JsonDocument document, string apiKey) =>
        Redact(document.RootElement.GetRawText(), apiKey);

    /// <summary>
    /// ActiveCampaign doesn't echo the token, but a misbehaving proxy might; the key is scrubbed from anything logged.
    /// </summary>
    private static string Redact(string body, string apiKey)
    {
        var key = apiKey.Trim();
        var safe = key.Length == 0 ? body : body.Replace(key, "[redacted]", StringComparison.Ordinal);
        return safe.Length <= MaxLoggedBodyLength ? safe : safe[..MaxLoggedBodyLength] + "…";
    }

    /// <summary>Null names are omitted from the JSON, so a re-submission without them doesn't blank the contact's existing name.</summary>
    private sealed record ContactPayload(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName);
}

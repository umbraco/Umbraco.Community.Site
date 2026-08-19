using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.FormsSpamGuard.Configuration;
using Umbraco.Community.FormsSpamGuard.Signals;
using Umbraco.Community.FormsSpamGuard.Tokens;
using Umbraco.Forms.Core.Attributes;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Services;

namespace Umbraco.Community.FormsSpamGuard.FieldTypes;

/// <summary>
/// A validation-only field that runs several independent bot checks against a submission without showing the
/// visitor anything.
/// </summary>
/// <remarks>
/// <para>
/// Place this field on the form's <b>final page</b> and <b>not inside a conditionally hidden fieldset</b>.
/// Umbraco Forms only validates fieldsets belonging to the step being submitted, and skips fieldsets hidden by a
/// condition, so a field placed elsewhere silently never runs.
/// </para>
/// <para>
/// Pages containing this field must not be output-cached: every visitor would then receive the same render
/// timestamp, and the maximum-age check would start rejecting genuine submissions once the cached entry aged past
/// <see cref="MaximumFormAgeHours"/>.
/// </para>
/// </remarks>
public class SpamGuardField : Umbraco.Forms.Core.FieldType
{
    private const string FillDurationItemKeyPrefix = "UmbracoCommunityFormsSpamGuard_FillDuration_";

    /// <summary>
    /// Values of the <c>Signal</c> log property that are not one of the <see cref="ISpamSignal"/> names.
    /// Documented in the README so operators can filter on them.
    /// </summary>
    internal static class RejectionSignals
    {
        /// <summary>No token posted at all — ordinary bot traffic.</summary>
        public const string TokenAbsent = "TokenAbsent";

        /// <summary>A token was posted but could not be decrypted — almost always a Data Protection key problem.</summary>
        public const string TokenUnreadable = "TokenUnreadable";
    }

    /// <summary>
    /// Values of the <c>Signal</c> log property on an acceptance, i.e. not the name of an <see cref="ISpamSignal"/>
    /// that tripped.
    /// </summary>
    internal static class AcceptanceReasons
    {
        /// <summary>Every signal ran and none of them objected.</summary>
        public const string Passed = "Passed";

        /// <summary>The editor switched off every signal, so the field is deliberately inert.</summary>
        public const string AllSignalsDisabled = "AllSignalsDisabled";
    }

    /// <summary>Scoped per field so two spam guard fields on one form cannot overwrite each other.</summary>
    private static string FillDurationItemKey(Field field) => FillDurationItemKeyPrefix + field.Id;

    /// <summary>
    /// Chosen to be meaningless to browser autofill heuristics, which read nearby label text as a signal.
    /// See <c>SpamGuardTokenService.DecoyNameCandidates</c> for the reasoning.
    /// </summary>
    internal const string DefaultDecoyFieldLabel = "Enquiry reference";

    private readonly ISpamGuardTokenService _tokenService;
    private readonly IEnumerable<ISpamSignal> _signals;
    private readonly ILogger<SpamGuardField> _logger;
    private readonly FormsSpamGuardOptions _options;
    private readonly TimeProvider _timeProvider;

    public SpamGuardField(
        ISpamGuardTokenService tokenService,
        IEnumerable<ISpamSignal> signals,
        ILogger<SpamGuardField> logger,
        IOptions<FormsSpamGuardOptions> options,
        TimeProvider timeProvider)
    {
        _tokenService = tokenService;
        _signals = signals.OrderBy(x => x.Order).ToArray();
        _logger = logger;
        _options = options.Value;
        _timeProvider = timeProvider;

        Id = new Guid(Constants.FieldTypeId);
        Name = "Spam guard";
        Alias = Constants.FieldTypeAlias;
        Description = "Invisible multi-signal bot check. Place on the form's last page, outside any conditional fieldset.";
        Icon = "icon-shield";
        DataType = FieldDataType.String;
        FieldTypeViewName = "FieldType.SpamGuard.cshtml";
        PreviewView = "UmbracoCommunity.FormsSpamGuard.FieldPreview";
        Category = "Common";
        SortOrder = 140;

        HideLabel = true;
        RenderInputType = RenderInputType.Custom;

        // The field never posts a value under its own key, so Forms' built-in mandatory check would always fail
        // for it. The base class defaults SupportsMandatory to true, which would let an editor tick a box that
        // does nothing today and would make the form permanently unsubmittable the moment anyone routed this
        // type through base.ValidateField. Take the checkbox away instead.
        SupportsMandatory = false;
        SupportsRegex = false;
    }

    [Setting(
        "Decoy field",
        Description = "Render a hidden decoy input with a randomised name and reject submissions that fill it in.",
        View = "Umb.PropertyEditorUi.Toggle",
        DisplayOrder = 10)]
    public virtual string EnableDecoyField { get; set; } = "True";

    [Setting(
        "Timing check",
        Description = "Reject submissions that arrive implausibly fast, or long after the form was rendered.",
        View = "Umb.PropertyEditorUi.Toggle",
        DisplayOrder = 20)]
    public virtual string EnableTimingCheck { get; set; } = "True";

    [Setting(
        "Require JavaScript",
        Description = "Reject submissions where the page's scripts did not run. This blocks visitors with JavaScript disabled from submitting at all, so leave it off unless you accept that.",
        View = "Umb.PropertyEditorUi.Toggle",
        DisplayOrder = 30)]
    public virtual string RequireJavaScript { get; set; } = string.Empty;

    [Setting(
        "Minimum fill time (seconds)",
        Description = "Submissions arriving sooner than this after the form was rendered are rejected. Three seconds is a safe starting point for a short form.",
        View = "Umb.PropertyEditorUi.Slider",
        PreValues = "0,60,1,3",
        DisplayOrder = 40)]
    public virtual string MinimumFillSeconds { get; set; } = string.Empty;

    [Setting(
        "Maximum form age (hours)",
        Description = "Submissions from a form rendered longer ago than this are rejected as replays or stale pages.",
        View = "Umb.PropertyEditorUi.Slider",
        PreValues = "1,24,1,2",
        DisplayOrder = 50)]
    public virtual string MaximumFormAgeHours { get; set; } = string.Empty;

    [Setting(
        "Decoy field label",
        Description = "The label given to the decoy input. No real visitor ever sees it, so pick for two audiences only: a bot's parser, and browser autofill. Avoid anything containing email, name, phone, address, company or website \u2014 autofill reads label text and would fill the decoy, rejecting a real submission.",
        View = "Umb.PropertyEditorUi.TextBox",
        DisplayOrder = 60)]
    public virtual string DecoyFieldLabel { get; set; } = string.Empty;

    [Setting(
        "Error message",
        Description = "Shown when a submission is rejected. Keep it generic: naming the check that caught it tells a bot exactly what to fix.",
        View = "Umb.PropertyEditorUi.TextArea",
        SupportsPlaceholders = true,
        DisplayOrder = 70)]
    public virtual string ErrorMessage { get; set; } = string.Empty;

    [Setting(
        "Save fill duration",
        Description = "Store how long the visitor took, e.g. \"71.8s\", against the submission. Useful for tuning the minimum fill time against real timings rather than guesswork. Shown in the entry under this field's caption, so give the field a caption that makes the number make sense.",
        View = "Umb.PropertyEditorUi.Toggle",
        DisplayOrder = 80)]
    public virtual string SaveFillDuration { get; set; } = string.Empty;

    /// <inheritdoc />
    public override bool StoresData => ParseBool(SaveFillDuration);

    /// <summary>
    /// Resolves the editor's string-valued settings into typed values.
    /// </summary>
    public SpamGuardFieldSettings ResolveSettings() => new()
    {
        EnableDecoyField = ParseBool(EnableDecoyField, defaultValue: true),
        EnableTimingCheck = ParseBool(EnableTimingCheck, defaultValue: true),
        RequireJavaScript = ParseBool(RequireJavaScript),
        MinimumFillTime = TimeSpan.FromSeconds(ParseDouble(MinimumFillSeconds, 3)),
        MaximumFormAge = TimeSpan.FromHours(ParseDouble(MaximumFormAgeHours, 2)),
        DecoyFieldLabel = string.IsNullOrWhiteSpace(DecoyFieldLabel) ? DefaultDecoyFieldLabel : DecoyFieldLabel,
        SaveFillDuration = ParseBool(SaveFillDuration),
    };

    /// <inheritdoc />
    public override IEnumerable<string> ValidateField(
        Form form,
        Field field,
        IEnumerable<object> postedValues,
        HttpContext context,
        IPlaceholderParsingService placeholderParsingService,
        IFieldTypeStorage fieldTypeStorage)
    {
        SpamGuardFieldSettings settings = ResolveSettings();

        // With every signal switched off the field is inert. Let the submission through rather than failing
        // closed on what is plainly a deliberate configuration — but still log it, the same as any other
        // outcome, so this doesn't read as "the field silently did nothing" to whoever is checking.
        if (_signals.Any(x => x.IsEnabled(settings)) == false)
        {
            Accept(form, field, AcceptanceReasons.AllSignalsDisabled, "Every signal is disabled on this field.");
            return [];
        }

        var fieldKey = field.Id.ToString();
        var protectedToken = ReadPostedValue(context, fieldKey + Constants.FormKeys.TokenSuffix);

        // These two are deliberately reported separately even though both reject. An absent token means the
        // submission never came from a form we rendered, which is ordinary bot traffic and expected background
        // noise. A token that is present but unreadable means this instance could not decrypt a payload we
        // almost certainly issued — that is an infrastructure problem, most often Data Protection keys not
        // being shared across instances, and it rejects *every* genuine submission for as long as it lasts.
        // Logging both under one message makes a total outage indistinguishable from a quiet day of bots.
        // IsNullOrWhiteSpace, not IsNullOrEmpty: TryUnprotect treats whitespace as absent too, so a bot
        // posting "{fieldId}_sg= " would otherwise fall through and raise the infrastructure alarm below.
        if (string.IsNullOrWhiteSpace(protectedToken))
        {
            return Reject(form, field, RejectionSignals.TokenAbsent,
                "No token was posted; the submission did not come from a rendered form.");
        }

        if (_tokenService.TryUnprotect(protectedToken, out SpamGuardToken? token) == false || token is null)
        {
            return Reject(
                form,
                field,
                RejectionSignals.TokenUnreadable,
                "A token was posted but could not be unprotected. If this is not isolated, check that Data "
                + "Protection keys are shared across instances — every submission will be rejected until they are.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var signalContext = new SpamSignalContext(token, settings, context, fieldKey, now);

        foreach (ISpamSignal signal in _signals)
        {
            if (signal.IsEnabled(settings) == false)
            {
                continue;
            }

            SpamSignalResult result = signal.Evaluate(signalContext);
            if (result.IsSpam)
            {
                return Reject(form, field, signal.Name, result.Reason ?? string.Empty);
            }
        }

        if (settings.SaveFillDuration)
        {
            context.Items[FillDurationItemKey(field)] = (now - token.RenderedUtc).TotalSeconds;
        }

        Accept(form, field, AcceptanceReasons.Passed, $"Decoy field: '{token.DecoyFieldName}'.");
        return [];
    }

    /// <inheritdoc />
    public override IEnumerable<object> ConvertToRecord(Field field, IEnumerable<object> postedValues, HttpContext context)
    {
        if (StoresData == false || context.Items.TryGetValue(FillDurationItemKey(field), out var duration) == false)
        {
            return base.ConvertToRecord(field, postedValues, context);
        }

        // Stored with its unit. The entry view labels this column with the editor's chosen field caption
        // ("Spam guard"), which says nothing about what the number is, so a bare "71.8" is unreadable to
        // whoever opens the record. The whole point of the value is a human eyeballing real timings to
        // tune the minimum, so readability beats keeping it machine-parseable in exports.
        return [((double)duration!).ToString("F1", CultureInfo.InvariantCulture) + "s"];
    }

    /// <summary>
    /// Records why a submission was rejected and returns the generic message shown to the visitor.
    /// </summary>
    /// <remarks>
    /// The visitor deliberately never learns which signal tripped. A bot that is told "you submitted too fast"
    /// simply waits; one told "leave that field alone" simply does. The log carries the real reason so the site
    /// owner can tune thresholds and spot false positives.
    /// </remarks>
    private string[] Reject(Form form, Field field, string signal, string reason)
    {
        // Signal is its own structured property rather than being folded into Reason, so the Umbraco log viewer
        // can filter on it directly (Signal = 'TokenUnreadable') instead of substring-matching free text. Since
        // a rejected submission is never stored, this log line is the only record that it happened.
        _logger.Log(
            _options.RejectionLogLevel,
            "Spam guard rejected a submission. Signal: {Signal}. Form: '{FormName}' ({FormId}), field: '{FieldCaption}'. {Reason}",
            signal,
            form.Name,
            form.Id,
            field.Caption,
            reason);

        return [string.IsNullOrWhiteSpace(ErrorMessage)
            ? "We couldn't process this submission. Please try again."
            : ErrorMessage];
    }

    /// <summary>
    /// Records that a submission passed. Without this, a rejection is the only outcome that leaves a trace, so
    /// "no log line" is ambiguous between "this passed" and "this field never ran" (wrong page, wrong fieldset,
    /// every signal disabled). Deliberately silent on which decoy name or timing passed — that's for tuning, not
    /// for a bot reading the response, and this never reaches the response anyway; it's a log-only record.
    /// </summary>
    private void Accept(Form form, Field field, string signal, string reason)
    {
        _logger.Log(
            _options.AcceptanceLogLevel,
            "Spam guard accepted a submission. Signal: {Signal}. Form: '{FormName}' ({FormId}), field: '{FieldCaption}'. {Reason}",
            signal,
            form.Name,
            form.Id,
            field.Caption,
            reason);
    }

    private static string? ReadPostedValue(HttpContext context, string key)
    {
        if (context.Request.HasFormContentType == false)
        {
            return null;
        }

        return context.Request.Form.TryGetValue(key, out var value) ? value.ToString() : null;
    }

    /// <summary>
    /// Forms stores toggle settings as the strings "True"/"False", and as an empty string when the editor has
    /// never touched the toggle — which is why an unset value falls back to the caller's default rather than false.
    /// </summary>
    private static bool ParseBool(string value, bool defaultValue = false) =>
        string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : string.Equals(value, "True", StringComparison.InvariantCultureIgnoreCase);

    private static double ParseDouble(string value, double defaultValue) =>
        double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
}

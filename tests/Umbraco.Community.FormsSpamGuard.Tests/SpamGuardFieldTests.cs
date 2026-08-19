using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Umbraco.Community.FormsSpamGuard.Configuration;
using Umbraco.Community.FormsSpamGuard.FieldTypes;
using Umbraco.Community.FormsSpamGuard.Signals;
using Umbraco.Community.FormsSpamGuard.Tokens;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Services;
using Xunit;

namespace Umbraco.Community.FormsSpamGuard.Tests;

public class SpamGuardFieldTests
{
    private static readonly Guid FieldId = new("11111111-1111-1111-1111-111111111111");

    private readonly SpamGuardTokenService _tokenService = TestHelpers.CreateTokenService();

    private SpamGuardField CreateField(
        DateTimeOffset? now = null,
        ILogger<SpamGuardField>? logger = null,
        params ISpamSignal[] signals) =>
        new(
            _tokenService,
            signals.Length > 0
                ? signals
                : [new DecoyFieldSignal(), new SubmissionTimingSignal(), new JavaScriptTokenSignal(_tokenService)],
            logger ?? NullLogger<SpamGuardField>.Instance,
            Options.Create(new FormsSpamGuardOptions()),
            new TestHelpers.FixedTimeProvider(now ?? TestHelpers.Now));

    private static HttpContext CreateRequest(Dictionary<string, string> postedValues)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(
            postedValues.ToDictionary(x => x.Key, x => new StringValues(x.Value)));
        return context;
    }

    private IEnumerable<string> Validate(SpamGuardField field, HttpContext context) =>
        field.ValidateField(
            new Form { Name = "Contact", Id = Guid.NewGuid() },
            new Field { Id = FieldId, Caption = "Spam guard" },
            [],
            context,
            Mock.Of<IPlaceholderParsingService>(),
            Mock.Of<IFieldTypeStorage>());

    [Fact]
    public void Accepts_a_clean_submission()
    {
        SpamGuardField field = CreateField();
        SpamGuardToken token = TestHelpers.CreateToken(renderedUtc: TestHelpers.Now.AddSeconds(-20));

        HttpContext context = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(token),
        });

        Validate(field, context).Should().BeEmpty();
    }

    [Fact]
    public void Rejects_a_submission_with_no_token_at_all()
    {
        // This is the shape of a bot posting straight to the endpoint without rendering the form.
        Validate(CreateField(), CreateRequest([])).Should().ContainSingle();
    }

    [Fact]
    public void Distinguishes_an_absent_token_from_one_it_cannot_unprotect()
    {
        // Both reject, but they mean opposite things: absent is ordinary bot traffic, unreadable is very likely
        // a Data Protection key problem that is rejecting every genuine submission. The visitor sees the same
        // generic message either way; the log must not.
        var logger = new CapturingLogger();
        SpamGuardField field = CreateField(logger: logger);

        Validate(field, CreateRequest([]));
        Validate(field, CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = "not-a-real-protected-value",
        }));

        logger.Messages.Should().HaveCount(2);
        logger.Messages[0].Should().Contain("TokenAbsent").And.Contain("No token was posted");
        logger.Messages[1].Should().Contain("TokenUnreadable").And.Contain("Data Protection keys are shared");
        logger.Messages[0].Should().NotBe(logger.Messages[1]);
    }

    [Fact]
    public void Rejects_a_submission_whose_token_was_tampered_with()
    {
        SpamGuardField field = CreateField();
        var protectedToken = _tokenService.Protect(TestHelpers.CreateToken());

        HttpContext context = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = protectedToken[..^4] + "AAAA",
        });

        Validate(field, context).Should().ContainSingle();
    }

    [Fact]
    public void Treats_a_whitespace_only_token_as_absent_rather_than_an_infrastructure_alarm()
    {
        // TryUnprotect treats whitespace as absent, so posting " " must not raise the "check your Data
        // Protection keys" alarm that operators are told to read as a live outage.
        var logger = new CapturingLogger();
        SpamGuardField field = CreateField(logger: logger);

        Validate(field, CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = "   ",
        })).Should().ContainSingle();

        logger.Messages.Should().ContainSingle().Which.Should().Contain("TokenAbsent");
    }

    [Fact]
    public void Records_the_signal_name_as_a_filterable_property()
    {
        // The log line is the only record a rejected submission leaves, so the signal has to be greppable.
        var logger = new CapturingLogger();
        SpamGuardField field = CreateField(logger: logger);

        SpamGuardToken token = TestHelpers.CreateToken(renderedUtc: TestHelpers.Now.AddSeconds(-20));
        Validate(field, CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(token),
            [token.DecoyFieldName] = "spam",
        }));

        logger.Messages.Should().ContainSingle().Which.Should().Contain(nameof(DecoyFieldSignal));
    }

    [Fact]
    public void Scopes_the_fill_duration_per_field()
    {
        // Two guards on one form must not overwrite each other's recorded duration.
        SpamGuardField field = CreateField();
        field.SaveFillDuration = "True";

        var otherFieldId = new Guid("22222222-2222-2222-2222-222222222222");
        SpamGuardToken token = TestHelpers.CreateToken(renderedUtc: TestHelpers.Now.AddSeconds(-42));
        HttpContext context = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(token),
        });

        Validate(field, context).Should().BeEmpty();

        // The other field never ran, so it must not inherit this field's duration.
        field.ConvertToRecord(new Field { Id = otherFieldId }, [], context).Should().BeEmpty();
        field.ConvertToRecord(new Field { Id = FieldId }, [], context)
            .Should().ContainSingle().Which.Should().Be("42.0s");
    }

    [Fact]
    public void Returns_the_same_generic_message_whichever_signal_trips()
    {
        // A bot must not be able to tell which check caught it by reading the response.
        SpamGuardField field = CreateField();

        var tooFast = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(TestHelpers.CreateToken()),
        });

        SpamGuardToken decoyToken = TestHelpers.CreateToken(renderedUtc: TestHelpers.Now.AddSeconds(-20));
        var decoyFilled = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(decoyToken),
            [decoyToken.DecoyFieldName] = "spam",
        });

        Validate(field, tooFast).Should().BeEquivalentTo(Validate(field, decoyFilled));
    }

    [Fact]
    public void Uses_the_configured_error_message_when_one_is_set()
    {
        SpamGuardField field = CreateField();
        field.ErrorMessage = "Sorry, please try again.";

        Validate(field, CreateRequest([])).Should().ContainSingle().Which.Should().Be("Sorry, please try again.");
    }

    [Fact]
    public void Skips_signals_the_editor_disabled()
    {
        SpamGuardField field = CreateField();
        field.EnableTimingCheck = "False";

        // Submitted instantly, which the timing signal would reject were it enabled.
        HttpContext context = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(TestHelpers.CreateToken()),
        });

        Validate(field, context).Should().BeEmpty();
    }

    [Fact]
    public void Lets_everything_through_when_every_signal_is_disabled()
    {
        // An inert field is a deliberate configuration, not a reason to fail closed and block the whole form.
        SpamGuardField field = CreateField();
        field.EnableDecoyField = "False";
        field.EnableTimingCheck = "False";
        field.RequireJavaScript = "False";

        Validate(field, CreateRequest([])).Should().BeEmpty();
    }

    [Fact]
    public void Logs_an_acceptance_when_every_signal_passes()
    {
        // A rejection was otherwise the only outcome that left a trace, which made "nothing logged" ambiguous
        // between "this passed" and "this field never ran at all" (wrong page, wrong fieldset, config error).
        var logger = new CapturingLogger();
        SpamGuardField field = CreateField(logger: logger);

        SpamGuardToken token = TestHelpers.CreateToken(renderedUtc: TestHelpers.Now.AddSeconds(-20));
        Validate(field, CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(token),
        })).Should().BeEmpty();

        logger.Messages.Should().ContainSingle().Which.Should().Contain("accepted").And.Contain("Passed");
    }

    [Fact]
    public void Logs_an_acceptance_when_every_signal_is_disabled()
    {
        var logger = new CapturingLogger();
        SpamGuardField field = CreateField(logger: logger);
        field.EnableDecoyField = "False";
        field.EnableTimingCheck = "False";
        field.RequireJavaScript = "False";

        Validate(field, CreateRequest([])).Should().BeEmpty();

        logger.Messages.Should().ContainSingle().Which.Should().Contain("accepted").And.Contain("AllSignalsDisabled");
    }

    [Fact]
    public void Evaluates_signals_in_order_and_stops_at_the_first_failure()
    {
        var recorder = new RecordingSignal();
        SpamGuardField field = CreateField(signals: [new AlwaysFailsSignal(), recorder]);

        HttpContext context = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(TestHelpers.CreateToken()),
        });

        Validate(field, context).Should().ContainSingle();
        recorder.WasEvaluated.Should().BeFalse("a later signal should not run once an earlier one has rejected");
    }

    [Fact]
    public void Does_not_let_an_editor_mark_the_field_mandatory_or_regex_validated()
    {
        // The field posts no value under its own key, so Forms' built-in mandatory check could never pass for it.
        // Leaving the base class default of SupportsMandatory = true would expose a checkbox that does nothing
        // today and would make the form unsubmittable if this type were ever routed through base.ValidateField.
        SpamGuardField field = CreateField();

        field.SupportsMandatory.Should().BeFalse();
        field.SupportsRegex.Should().BeFalse();
    }

    [Fact]
    public void Default_decoy_label_is_inert_to_browser_autofill()
    {
        // Autofill reads nearby label text. A label like "Confirm email address" would invite a browser or
        // password manager to fill the decoy and get a real visitor's submission rejected.
        var label = CreateField().ResolveSettings().DecoyFieldLabel.ToLowerInvariant();

        foreach (var token in new[]
                 {
                     "email", "name", "phone", "tel", "address", "city", "zip", "postal",
                     "country", "company", "organi", "url", "website", "homepage", "card",
                     "username", "password",
                 })
        {
            label.Should().NotContain(token, $"'{token}' is browser autofill vocabulary");
        }
    }

    [Fact]
    public void ResolveSettings_falls_back_to_defaults_for_untouched_toggles()
    {
        // Forms stores an untouched toggle as an empty string. The decoy and timing checks must default to on,
        // and RequireJavaScript to off, rather than all collapsing to false.
        SpamGuardFieldSettings settings = CreateField().ResolveSettings();

        settings.EnableDecoyField.Should().BeTrue();
        settings.EnableTimingCheck.Should().BeTrue();
        settings.RequireJavaScript.Should().BeFalse();
        settings.MinimumFillTime.Should().Be(TimeSpan.FromSeconds(3));
        settings.MaximumFormAge.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void ResolveSettings_parses_numbers_invariantly()
    {
        // Forms persists slider values in the invariant culture; parsing under a comma-decimal culture must not
        // silently fall back to the default.
        SpamGuardField field = CreateField();
        field.MinimumFillSeconds = "7.5";

        field.ResolveSettings().MinimumFillTime.Should().Be(TimeSpan.FromSeconds(7.5));
    }

    [Fact]
    public void StoresData_follows_the_save_fill_duration_setting()
    {
        SpamGuardField field = CreateField();
        field.StoresData.Should().BeFalse();

        field.SaveFillDuration = "True";
        field.StoresData.Should().BeTrue();
    }

    [Fact]
    public void Records_the_fill_duration_when_asked_to()
    {
        SpamGuardField field = CreateField();
        field.SaveFillDuration = "True";

        SpamGuardToken token = TestHelpers.CreateToken(renderedUtc: TestHelpers.Now.AddSeconds(-42));
        HttpContext context = CreateRequest(new Dictionary<string, string>
        {
            [FieldId + Constants.FormKeys.TokenSuffix] = _tokenService.Protect(token),
        });

        Validate(field, context).Should().BeEmpty();

        field.ConvertToRecord(new Field { Id = FieldId }, [], context)
            .Should().ContainSingle().Which.Should().Be("42.0s");
    }

    /// <summary>Captures formatted log messages so tests can assert on what an operator would actually see.</summary>
    private sealed class CapturingLogger : ILogger<SpamGuardField>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class AlwaysFailsSignal : ISpamSignal
    {
        public string Name => nameof(AlwaysFailsSignal);
        public int Order => 1;
        public bool IsEnabled(SpamGuardFieldSettings settings) => true;
        public SpamSignalResult Evaluate(SpamSignalContext context) => SpamSignalResult.Fail("always");
    }

    private sealed class RecordingSignal : ISpamSignal
    {
        public bool WasEvaluated { get; private set; }
        public string Name => nameof(RecordingSignal);
        public int Order => 2;
        public bool IsEnabled(SpamGuardFieldSettings settings) => true;

        public SpamSignalResult Evaluate(SpamSignalContext context)
        {
            WasEvaluated = true;
            return SpamSignalResult.Pass();
        }
    }
}

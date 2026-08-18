using FluentAssertions;
using Umbraco.Community.FormsSpamGuard.Signals;
using Umbraco.Community.FormsSpamGuard.Tokens;
using Xunit;

namespace Umbraco.Community.FormsSpamGuard.Tests;

public class DecoyFieldSignalTests
{
    private readonly DecoyFieldSignal _signal = new();

    [Fact]
    public void Passes_when_the_decoy_was_left_alone()
    {
        SpamSignalContext context = TestHelpers.CreateContext(
            postedValues: new Dictionary<string, string> { ["emailConfirm_abc123"] = string.Empty });

        _signal.Evaluate(context).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Passes_when_the_decoy_key_is_absent_entirely()
    {
        // A form posted without the field at all is odd but not evidence of a bot; a stale cached page can do it.
        _signal.Evaluate(TestHelpers.CreateContext()).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Passes_when_the_decoy_holds_only_whitespace()
    {
        // Guards against an autofill that writes a space costing someone a real enquiry.
        SpamSignalContext context = TestHelpers.CreateContext(
            postedValues: new Dictionary<string, string> { ["emailConfirm_abc123"] = "   " });

        _signal.Evaluate(context).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_decoy_was_completed()
    {
        SpamSignalContext context = TestHelpers.CreateContext(
            postedValues: new Dictionary<string, string> { ["emailConfirm_abc123"] = "http://spam.example" });

        SpamSignalResult result = _signal.Evaluate(context);

        result.IsSpam.Should().BeTrue();
        result.Reason.Should().Contain("emailConfirm_abc123");
    }

    [Fact]
    public void Checks_the_name_carried_in_the_token_not_a_fixed_one()
    {
        // The decoy name is randomised per render and travels in the signed token; the signal must read it from
        // there rather than assume any particular name.
        SpamSignalContext context = TestHelpers.CreateContext(
            token: TestHelpers.CreateToken(decoyFieldName: "faxNumber_deadbe"),
            postedValues: new Dictionary<string, string> { ["faxNumber_deadbe"] = "filled" });

        _signal.Evaluate(context).IsSpam.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Is_enabled_according_to_settings(bool enabled) =>
        _signal.IsEnabled(new SpamGuardFieldSettings { EnableDecoyField = enabled }).Should().Be(enabled);
}

public class SubmissionTimingSignalTests
{
    private readonly SubmissionTimingSignal _signal = new();

    private static SpamSignalContext ContextForElapsed(TimeSpan elapsed, SpamGuardFieldSettings? settings = null) =>
        TestHelpers.CreateContext(
            token: TestHelpers.CreateToken(renderedUtc: TestHelpers.Now - elapsed),
            settings: settings,
            now: TestHelpers.Now);

    [Fact]
    public void Passes_for_a_plausible_fill_time()
    {
        _signal.Evaluate(ContextForElapsed(TimeSpan.FromSeconds(30))).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_submitted_faster_than_the_minimum()
    {
        SpamSignalResult result = _signal.Evaluate(ContextForElapsed(TimeSpan.FromSeconds(1)));

        result.IsSpam.Should().BeTrue();
        result.Reason.Should().Contain("minimum");
    }

    [Fact]
    public void Passes_exactly_at_the_minimum()
    {
        // The bound is exclusive: three seconds flat is acceptable when the minimum is three seconds.
        _signal.Evaluate(ContextForElapsed(TimeSpan.FromSeconds(3))).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_form_is_older_than_the_maximum()
    {
        SpamSignalResult result = _signal.Evaluate(ContextForElapsed(TimeSpan.FromHours(3)));

        result.IsSpam.Should().BeTrue();
        result.Reason.Should().Contain("maximum");
    }

    [Fact]
    public void Passes_exactly_at_the_maximum()
    {
        _signal.Evaluate(ContextForElapsed(TimeSpan.FromHours(2))).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Honours_configured_bounds()
    {
        var settings = new SpamGuardFieldSettings
        {
            MinimumFillTime = TimeSpan.FromSeconds(10),
            MaximumFormAge = TimeSpan.FromMinutes(30),
        };

        _signal.Evaluate(ContextForElapsed(TimeSpan.FromSeconds(5), settings)).IsSpam.Should().BeTrue();
        _signal.Evaluate(ContextForElapsed(TimeSpan.FromMinutes(45), settings)).IsSpam.Should().BeTrue();
        _signal.Evaluate(ContextForElapsed(TimeSpan.FromMinutes(5), settings)).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Tolerates_small_clock_skew_between_instances()
    {
        // A token issued a few seconds "in the future" by another instance with a slightly fast clock must not
        // cost a real submission.
        _signal.Evaluate(ContextForElapsed(TimeSpan.FromSeconds(-5))).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_token_issued_implausibly_far_in_the_future()
    {
        _signal.Evaluate(ContextForElapsed(TimeSpan.FromMinutes(-10))).IsSpam.Should().BeTrue();
    }
}

public class JavaScriptTokenSignalTests
{
    private const string FieldKey = "11111111-1111-1111-1111-111111111111";

    private readonly ISpamGuardTokenService _tokenService = TestHelpers.CreateTokenService();
    private readonly JavaScriptTokenSignal _signal;

    public JavaScriptTokenSignalTests() => _signal = new JavaScriptTokenSignal(_tokenService);

    [Fact]
    public void Passes_when_the_expected_answer_was_written()
    {
        SpamGuardToken token = TestHelpers.CreateToken();
        var answer = _tokenService.ComputeJavaScriptAnswer(token.Nonce);

        SpamSignalContext context = TestHelpers.CreateContext(
            token: token,
            postedValues: new Dictionary<string, string> { [FieldKey + Constants.FormKeys.JavaScriptSuffix] = answer });

        _signal.Evaluate(context).IsSpam.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_input_was_never_filled()
    {
        SpamSignalResult result = _signal.Evaluate(TestHelpers.CreateContext());

        result.IsSpam.Should().BeTrue();
        result.Reason.Should().Contain("scripts did not run");
    }

    [Fact]
    public void Fails_when_the_answer_is_wrong()
    {
        SpamSignalContext context = TestHelpers.CreateContext(
            postedValues: new Dictionary<string, string> { [FieldKey + Constants.FormKeys.JavaScriptSuffix] = "guessed" });

        _signal.Evaluate(context).IsSpam.Should().BeTrue();
    }

    [Fact]
    public void Is_off_by_default()
    {
        // Enabling it stops visitors without JavaScript submitting at all, so it must never be on unless asked for.
        _signal.IsEnabled(new SpamGuardFieldSettings()).Should().BeFalse();
    }
}

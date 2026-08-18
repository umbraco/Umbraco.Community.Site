using Umbraco.Community.FormsSpamGuard.Tokens;

namespace Umbraco.Community.FormsSpamGuard.Signals;

/// <summary>
/// Trips when the proof-of-presence input does not carry the value the shipped script would have written.
/// </summary>
/// <remarks>
/// This proves that a JavaScript engine executed on the page and nothing more. The algorithm is public, so a
/// determined bot can replicate it; what it reliably excludes is the much larger population of scrapers that
/// fetch and parse without running scripts. It is off by default because enabling it stops anyone with
/// JavaScript disabled from submitting the form at all.
/// </remarks>
public sealed class JavaScriptTokenSignal : ISpamSignal
{
    private readonly ISpamGuardTokenService _tokenService;

    public JavaScriptTokenSignal(ISpamGuardTokenService tokenService) => _tokenService = tokenService;

    /// <inheritdoc />
    public string Name => nameof(JavaScriptTokenSignal);

    /// <inheritdoc />
    public int Order => 30;

    /// <inheritdoc />
    public bool IsEnabled(SpamGuardFieldSettings settings) => settings.RequireJavaScript;

    /// <inheritdoc />
    public SpamSignalResult Evaluate(SpamSignalContext context)
    {
        var posted = context.GetPostedValue(context.FieldKey + Constants.FormKeys.JavaScriptSuffix);

        if (string.IsNullOrEmpty(posted))
        {
            return SpamSignalResult.Fail("Proof-of-presence value was absent; scripts did not run.");
        }

        var expected = _tokenService.ComputeJavaScriptAnswer(context.Token.Nonce);

        return string.Equals(posted, expected, StringComparison.Ordinal)
            ? SpamSignalResult.Pass()
            : SpamSignalResult.Fail("Proof-of-presence value did not match the expected answer.");
    }
}

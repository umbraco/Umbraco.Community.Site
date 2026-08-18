namespace Umbraco.Community.FormsSpamGuard.Signals;

/// <summary>
/// Trips when a form is submitted implausibly quickly after being rendered, or so long afterwards that the token
/// has most likely been replayed or served from a stale cache.
/// </summary>
public sealed class SubmissionTimingSignal : ISpamSignal
{
    /// <inheritdoc />
    public string Name => nameof(SubmissionTimingSignal);

    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public bool IsEnabled(SpamGuardFieldSettings settings) => settings.EnableTimingCheck;

    /// <inheritdoc />
    public SpamSignalResult Evaluate(SpamSignalContext context)
    {
        TimeSpan elapsed = context.Now - context.Token.RenderedUtc;

        // A negative elapsed time means the token was issued in the future, which server clock skew across
        // instances can cause legitimately. Treat it as suspicious only if it is beyond a small tolerance.
        if (elapsed < TimeSpan.Zero)
        {
            return elapsed < -ClockSkewTolerance
                ? SpamSignalResult.Fail($"Token was issued {-elapsed.TotalSeconds:F1}s in the future.")
                : SpamSignalResult.Pass();
        }

        if (elapsed < context.Settings.MinimumFillTime)
        {
            return SpamSignalResult.Fail(
                $"Submitted after {elapsed.TotalSeconds:F1}s, below the {context.Settings.MinimumFillTime.TotalSeconds:F0}s minimum.");
        }

        if (elapsed > context.Settings.MaximumFormAge)
        {
            return SpamSignalResult.Fail(
                $"Submitted after {elapsed.TotalHours:F1}h, beyond the {context.Settings.MaximumFormAge.TotalHours:F0}h maximum.");
        }

        return SpamSignalResult.Pass();
    }

    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromSeconds(30);
}

namespace Umbraco.Community.FormsSpamGuard.Signals;

/// <summary>
/// A single independent check against a form submission.
/// </summary>
/// <remarks>
/// Signals are deliberately kept behind an interface rather than folded into the field type. It costs little and
/// buys two things: each check can be unit tested without building a field type and a request pipeline around it,
/// and letting consumers register their own checks later becomes an additive change instead of a rewrite.
/// </remarks>
public interface ISpamSignal
{
    /// <summary>
    /// Stable name used in log messages. Not shown to visitors.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The order signals run in. Cheaper checks run first so an obvious bot is rejected without doing more work.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Whether the editor has enabled this signal on the form being validated.
    /// </summary>
    bool IsEnabled(SpamGuardFieldSettings settings);

    /// <summary>
    /// Judges the submission.
    /// </summary>
    SpamSignalResult Evaluate(SpamSignalContext context);
}

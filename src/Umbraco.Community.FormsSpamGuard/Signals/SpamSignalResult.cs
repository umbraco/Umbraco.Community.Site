namespace Umbraco.Community.FormsSpamGuard.Signals;

/// <summary>
/// The outcome of evaluating a single signal.
/// </summary>
/// <param name="IsSpam">Whether the signal considers the submission to be automated.</param>
/// <param name="Reason">
/// Why the signal tripped. Written to the log, never shown to the visitor — telling a bot which of the three
/// checks caught it is free intelligence.
/// </param>
public readonly record struct SpamSignalResult(bool IsSpam, string? Reason)
{
    /// <summary>The submission looks human as far as this signal is concerned.</summary>
    public static SpamSignalResult Pass() => new(false, null);

    /// <summary>The submission looks automated.</summary>
    public static SpamSignalResult Fail(string reason) => new(true, reason);
}

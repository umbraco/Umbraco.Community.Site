namespace Umbraco.Community.FormsSpamGuard.Signals;

/// <summary>
/// Trips when the hidden decoy input comes back with a value. A human never sees the field, so anything in it was
/// put there by something filling inputs indiscriminately.
/// </summary>
public sealed class DecoyFieldSignal : ISpamSignal
{
    /// <inheritdoc />
    public string Name => nameof(DecoyFieldSignal);

    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public bool IsEnabled(SpamGuardFieldSettings settings) => settings.EnableDecoyField;

    /// <inheritdoc />
    public SpamSignalResult Evaluate(SpamSignalContext context)
    {
        var value = context.GetPostedValue(context.Token.DecoyFieldName);

        // An absent key is normal: browsers omit nothing here, but a form posted without the field at all (an
        // older cached page, say) should not be punished by this signal. Only a value present and non-blank is
        // evidence. Whitespace counts as blank so an autofill that writes " " does not cost a real enquiry.
        return string.IsNullOrWhiteSpace(value)
            ? SpamSignalResult.Pass()
            : SpamSignalResult.Fail($"Decoy field '{context.Token.DecoyFieldName}' was completed.");
    }
}

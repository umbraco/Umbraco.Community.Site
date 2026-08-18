namespace Umbraco.Community.FormsSpamGuard.Signals;

/// <summary>
/// The per-form settings an editor configured on the field, resolved from the field type's string-valued
/// <c>[Setting]</c> properties into typed values.
/// </summary>
/// <remarks>
/// Forms persists every setting as a string. Parsing once into this type keeps that concern out of the signals,
/// which is what makes them testable without constructing a field type.
/// </remarks>
public sealed record SpamGuardFieldSettings
{
    /// <summary>Whether the decoy input is rendered and checked.</summary>
    public bool EnableDecoyField { get; init; } = true;

    /// <summary>Whether the render timestamp is checked.</summary>
    public bool EnableTimingCheck { get; init; } = true;

    /// <summary>Whether a correct proof-of-presence answer is required.</summary>
    public bool RequireJavaScript { get; init; }

    /// <summary>Submissions faster than this are rejected.</summary>
    public TimeSpan MinimumFillTime { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Submissions from a form rendered longer ago than this are rejected.</summary>
    public TimeSpan MaximumFormAge { get; init; } = TimeSpan.FromHours(2);

    /// <summary>Label text rendered against the decoy input.</summary>
    public string DecoyFieldLabel { get; init; } = "Enquiry reference";

    /// <summary>Whether the fill duration is stored with the record on success.</summary>
    public bool SaveFillDuration { get; init; }
}

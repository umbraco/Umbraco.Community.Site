namespace Umbraco.Community.FormsSpamGuard.Tokens;

/// <summary>
/// The payload carried in the form's protected hidden input.
/// </summary>
/// <remarks>
/// Bundling the decoy field's name into the signed payload is what allows that name to be randomised on every
/// render: the server does not need to derive or remember it, because the submission carries it back in a form
/// the client cannot tamper with.
/// </remarks>
/// <param name="RenderedUtc">When the form was rendered, used for the timing checks.</param>
/// <param name="DecoyFieldName">The randomly generated name of the decoy input for this render.</param>
/// <param name="Nonce">Random value the proof-of-presence script derives its answer from.</param>
public sealed record SpamGuardToken(
    DateTimeOffset RenderedUtc,
    string DecoyFieldName,
    string Nonce);

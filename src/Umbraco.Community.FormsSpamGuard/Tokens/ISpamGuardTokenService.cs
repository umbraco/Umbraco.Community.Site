namespace Umbraco.Community.FormsSpamGuard.Tokens;

/// <summary>
/// Issues and verifies the protected token that ties a rendered form to its submission.
/// </summary>
public interface ISpamGuardTokenService
{
    /// <summary>
    /// Creates a token for a form being rendered now, with a freshly randomised decoy name and nonce.
    /// </summary>
    SpamGuardToken Issue();

    /// <summary>
    /// Serialises and protects a token for embedding in the form.
    /// </summary>
    string Protect(SpamGuardToken token);

    /// <summary>
    /// Attempts to unprotect and deserialise a token from a submitted value. Returns <c>false</c> for values that
    /// are absent, malformed, tampered with, or protected under a different purpose.
    /// </summary>
    bool TryUnprotect(string? protectedValue, out SpamGuardToken? token);

    /// <summary>
    /// Computes the value the proof-of-presence script is expected to have written for the given nonce.
    /// </summary>
    string ComputeJavaScriptAnswer(string nonce);
}

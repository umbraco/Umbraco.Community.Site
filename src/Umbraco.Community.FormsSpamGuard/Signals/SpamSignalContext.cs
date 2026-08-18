using Microsoft.AspNetCore.Http;
using Umbraco.Community.FormsSpamGuard.Tokens;

namespace Umbraco.Community.FormsSpamGuard.Signals;

/// <summary>
/// Everything a signal needs to judge one submission.
/// </summary>
/// <param name="Token">The verified token issued when the form was rendered.</param>
/// <param name="Settings">The editor's per-form configuration.</param>
/// <param name="HttpContext">The request being validated, for reading posted values.</param>
/// <param name="FieldKey">
/// The field's GUID as a string. This is the key Forms posts values under, and it is deliberately not the
/// field's HTML id: <c>FieldViewModel.Id</c> is prefixed by <c>FormDesignSettings.FormElementHtmlIdPrefix</c>
/// while <c>FieldViewModel.Name</c> (and the posted key) is not, so using the id would break input-name
/// matching on any site that configures a prefix.
/// </param>
/// <param name="Now">The current time, injected so timing behaviour is testable.</param>
public sealed record SpamSignalContext(
    SpamGuardToken Token,
    SpamGuardFieldSettings Settings,
    HttpContext HttpContext,
    string FieldKey,
    DateTimeOffset Now)
{
    /// <summary>
    /// Reads a posted form value, returning <c>null</c> when the request is not form-encoded or the key is absent.
    /// </summary>
    public string? GetPostedValue(string key)
    {
        if (HttpContext.Request.HasFormContentType == false)
        {
            return null;
        }

        return HttpContext.Request.Form.TryGetValue(key, out var value) ? value.ToString() : null;
    }
}

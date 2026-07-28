using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;

namespace UmbracoCommunity.BlogAnnouncements.Automate;

/// <summary>Settings for <see cref="HttpGetAction"/>.</summary>
public sealed class HttpGetSettings
{
    [Field(Label = "URL", Description = "The URL to send the GET request to.", SupportsBindings = true)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// A single-value field intended to hold a bare <c>$Key:Path</c> config reference (e.g.
    /// <c>$CommunityBlogs:ApiKey</c>), never a literal secret. Deliberately NOT marked
    /// <c>IsSensitive</c>: Deploy's automation-artifact exporter strips any step setting flagged
    /// <c>IsSensitive</c> unconditionally, regardless of whether the stored value is an actual secret
    /// or just a config-reference pointer — so a sensitive field here would lose its
    /// <c>$CommunityBlogs:ApiKey</c> value on every export/import round-trip. Since the pointer string
    /// itself isn't secret (the real key lives only in appsettings.Local.json/env vars, never in
    /// Automate's DB), leaving the field non-sensitive is safe for its intended use — see
    /// <c>Umbraco:Automate:AllowedConfigurationKeyPrefixes</c> in appsettings.json, which gates which
    /// keys a non-sensitive field may reference this way. The trade-off: if someone types a literal
    /// secret here instead of a $-reference, it's stored in plaintext and included in Deploy exports.
    /// </summary>
    [Field(
        Label = "Authorization Header Value",
        Description = "Value for the Authorization header — must be a $Key:Path config reference (e.g. $CommunityBlogs:ApiKey), not a literal secret (this field is not encrypted and is included in Deploy exports).",
        SupportsBindings = true)]
    public string? AuthorizationHeaderValue { get; set; }

    [Field(Label = "Headers", Description = "Additional custom headers as JSON object (e.g. {\"Accept\": \"application/json\"}). Not for the Authorization header — use the field above.", SupportsBindings = true)]
    public string? Headers { get; set; }
}

/// <summary>Output for <see cref="HttpGetAction"/>.</summary>
public sealed class HttpGetOutput
{
    public int StatusCode { get; init; }
    public string? ResponseBody { get; init; }
}

/// <summary>
/// A plain GET request via a default <see cref="HttpClient"/> — no SSRF connect-callback, unlike
/// the built-in "HTTP Request" action. Exists specifically to give the Authorization value its own
/// single-value field: Automate's sensitive-field decrypt-on-execution is currently unreliable for
/// a literal secret (works once, then every subsequent execution — scheduled or manual — receives
/// the raw <c>ENC:...</c> ciphertext instead of the decrypted value), but a <c>$Key:Path</c> config
/// reference sidesteps that entirely, since it's never encrypted in the first place. See
/// <see cref="HttpGetSettings.AuthorizationHeaderValue"/>'s doc comment.
/// </summary>
[Action(
    "umbracoCommunity.httpGet",
    "HTTP GET",
    Description = "A plain GET request with a non-encrypted Headers field — use for calls needing an API key, since Automate's sensitive-field decryption is currently unreliable at execution time.",
    Group = "Automation",
    Icon = "icon-cloud-upload")]
public sealed class HttpGetAction : ActionBase<HttpGetSettings, HttpGetOutput>
{
    private static readonly HttpClient Client = new();

    public HttpGetAction(ActionInfrastructure infrastructure) : base(infrastructure)
    {
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<HttpGetSettings>();
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            return ActionResult.Failed(new ArgumentException("URL is required."), StepRunErrorCategory.Validation);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, settings.Url);
        if (!string.IsNullOrWhiteSpace(settings.Headers))
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(settings.Headers);
            if (headers is not null)
            {
                foreach (var (name, value) in headers)
                {
                    request.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.AuthorizationHeaderValue))
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", settings.AuthorizationHeaderValue);
        }

        using var response = await Client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var output = new HttpGetOutput
        {
            StatusCode = (int)response.StatusCode,
            ResponseBody = body,
        };

        return response.IsSuccessStatusCode
            ? Success(output)
            : ActionResult.Failed(
                new HttpRequestException($"HTTP {(int)response.StatusCode}: {body}"),
                StepRunErrorCategory.InvalidResponse);
    }
}

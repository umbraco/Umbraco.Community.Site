using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Umbraco.Community.FormsSpamGuard.Configuration;

namespace Umbraco.Community.FormsSpamGuard.Tokens;

/// <inheritdoc />
public sealed class SpamGuardTokenService : ISpamGuardTokenService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Names the decoy could be given.
    /// </summary>
    /// <remarks>
    /// Every candidate is deliberately <b>inert to browser autofill</b>. Plausible-looking names are tempting —
    /// a scraper that skips fields by name should have to skip something a real form might contain — but names
    /// like <c>emailConfirm</c>, <c>websiteUrl</c> or <c>faxNumber</c> sit squarely in the autofill vocabulary
    /// (<c>email</c>, <c>url</c>, <c>tel</c>, <c>additional-name</c>). Chrome ignores <c>autocomplete="off"</c>
    /// for those heuristics and password managers ignore it outright, so a real visitor's browser would fill the
    /// decoy and their enquiry would be rejected.
    ///
    /// The asymmetry decides it: a missed bot costs one spam message, a false positive costs a real person's
    /// enquiry with no explanation. So these read as internal reference fields — plausible enough that filling
    /// every input still trips them, meaningless to any autofill heuristic.
    ///
    /// Do not add anything containing email, name, phone/tel, address, city, zip/postal, country,
    /// company/organization, url/website/homepage, card/cc, username or password.
    /// </remarks>
    private static readonly string[] DecoyNameCandidates =
    [
        "enquiryReference", "topicCode", "caseRef", "messageCategory", "preferredContactWindow",
        "referralCode", "routingKey", "submissionTag", "priorityBand", "sourceLabel",
    ];

    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public SpamGuardTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<FormsSpamGuardOptions> options,
        TimeProvider timeProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(options.Value.DataProtectionPurpose);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public SpamGuardToken Issue()
    {
        var name = DecoyNameCandidates[RandomNumberGenerator.GetInt32(DecoyNameCandidates.Length)];

        // Suffix the chosen name so the same candidate never produces the same key twice. Without this a bot
        // could collect the ten candidates once and exclude them permanently.
        var suffix = RandomNumberGenerator.GetHexString(6, lowercase: true);

        return new SpamGuardToken(
            _timeProvider.GetUtcNow(),
            $"{name}_{suffix}",
            RandomNumberGenerator.GetHexString(16, lowercase: true));
    }

    /// <inheritdoc />
    public string Protect(SpamGuardToken token)
    {
        var payload = new TokenPayload
        {
            T = token.RenderedUtc.ToUnixTimeSeconds(),
            D = token.DecoyFieldName,
            N = token.Nonce,
        };

        return _protector.Protect(JsonSerializer.Serialize(payload, SerializerOptions));
    }

    /// <inheritdoc />
    public bool TryUnprotect(string? protectedValue, out SpamGuardToken? token)
    {
        token = null;

        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return false;
        }

        try
        {
            var json = _protector.Unprotect(protectedValue);
            TokenPayload? payload = JsonSerializer.Deserialize<TokenPayload>(json, SerializerOptions);

            if (payload is null || string.IsNullOrEmpty(payload.D) || string.IsNullOrEmpty(payload.N))
            {
                return false;
            }

            token = new SpamGuardToken(
                DateTimeOffset.FromUnixTimeSeconds(payload.T),
                payload.D,
                payload.N);

            return true;
        }
        catch (CryptographicException)
        {
            // Tampered, truncated, or protected under a different purpose or key ring.
            return false;
        }
        catch (JsonException)
        {
            // Unprotected cleanly but is not a payload we recognise.
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is obfuscation, not cryptography, and it is not trying to be otherwise. The algorithm ships in a
    /// public package, so anyone can replicate it; all a correct answer proves is that a JavaScript engine ran on
    /// the page. That still rules out the large population of scrapers that fetch and parse without executing
    /// scripts, which is the entire point. It must stay cheap enough to reimplement in a few lines of browser JS
    /// — see wwwroot/App_Plugins/UmbracoCommunityFormsSpamGuard/spam-guard.js, which must be kept in step.
    /// </remarks>
    public string ComputeJavaScriptAnswer(string nonce)
    {
        if (string.IsNullOrEmpty(nonce))
        {
            return string.Empty;
        }

        var reversed = new string(nonce.Reverse().ToArray());
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(reversed))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Wire format for the protected payload. Single-character names keep the protected string short, since it is
    /// embedded in every rendered form.
    /// </summary>
    private sealed class TokenPayload
    {
        [JsonPropertyName("t")]
        public long T { get; set; }

        [JsonPropertyName("d")]
        public string D { get; set; } = string.Empty;

        [JsonPropertyName("n")]
        public string N { get; set; } = string.Empty;
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace UmbracoCommunity.Web.Features.WebBotAuth;

public sealed record SignedKeyDirectory(string Body, string ContentDigest, string SignatureInput, string Signature);

/// <summary>
/// Builds the Web Bot Auth key directory (a JWKS) and signs the response per RFC 9421 so
/// Cloudflare can verify it. The signature is computed per call with a short expiry.
/// </summary>
public sealed class KeyDirectoryService
{
    public const string Tag = "http-message-signatures-directory";

    private readonly Ed25519PrivateKeyParameters? _privateKey;
    private readonly string? _directoryJson;
    private readonly string? _keyId;
    private readonly WebBotAuthOptions _options;
    private readonly TimeProvider _timeProvider;

    public KeyDirectoryService(IOptions<WebBotAuthOptions> options, TimeProvider timeProvider, ILogger<KeyDirectoryService> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;

        if (string.IsNullOrWhiteSpace(_options.SigningPrivateKey))
        {
            return;
        }

        try
        {
            _privateKey = ParsePrivateKey(_options.SigningPrivateKey);
            var x = Base64Url(_privateKey.GeneratePublicKey().GetEncoded());
            _keyId = Thumbprint(x);
            _directoryJson = $"{{\"keys\":[{{\"kty\":\"OKP\",\"crv\":\"Ed25519\",\"x\":\"{x}\",\"kid\":\"{_keyId}\"}}]}}";
        }
        catch (Exception ex)
        {
            // Never log the key material itself.
            logger.LogError(ex, "WebBotAuth:SigningPrivateKey is set but is not a valid Ed25519 PKCS8 PEM key; the key directory is disabled");
            _privateKey = null;
        }
    }

    public bool IsConfigured => _privateKey is not null;

    public int CacheMaxAgeSeconds => _options.CacheMaxAgeSeconds;

    public SignedKeyDirectory Sign(string authority)
    {
        if (_privateKey is null || _directoryJson is null || _keyId is null)
        {
            throw new InvalidOperationException("No signing key configured.");
        }

        var created = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var expires = created + _options.SignatureLifetimeSeconds;

        // RFC 9530 digest of the exact body bytes; Cloudflare's validator requires it and a
        // signature that covers it, so a cached or tampered body can't ride on the signature.
        var bodyBytes = Encoding.UTF8.GetBytes(_directoryJson);
        var contentDigest = $"sha-256=:{Convert.ToBase64String(SHA256.HashData(bodyBytes))}:";

        // Signature parameters: covered components, then alg/keyid/created/expires/tag.
        var parameters = $"(\"@authority\";req \"content-digest\");alg=\"ed25519\";keyid=\"{_keyId}\";created={created};expires={expires};tag=\"{Tag}\"";
        var signatureBase = $"\"@authority\";req: {authority}\n\"content-digest\": {contentDigest}\n\"@signature-params\": {parameters}";

        var signer = new Ed25519Signer();
        signer.Init(true, _privateKey);
        var data = Encoding.UTF8.GetBytes(signatureBase);
        signer.BlockUpdate(data, 0, data.Length);

        return new SignedKeyDirectory(
            _directoryJson,
            contentDigest,
            $"sig1={parameters}",
            $"sig1=:{Convert.ToBase64String(signer.GenerateSignature())}:");
    }

    private static Ed25519PrivateKeyParameters ParsePrivateKey(string value)
    {
        // Secrets get pasted in many shapes: a real multi-line PEM, a JSON-escaped one with
        // literal "\n" text, wrapped in quotes, or just the base64 body. Strip the PEM armour
        // and every non-base64 character (quotes, whitespace, literal "\n"), then rebuild it.
        var body = value
            .Replace("\\r", string.Empty)
            .Replace("\\n", string.Empty)
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
            .Replace("-----END PRIVATE KEY-----", string.Empty);
        body = new string(body.Where(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '=').ToArray());

        using var reader = new StringReader($"-----BEGIN PRIVATE KEY-----\n{body}\n-----END PRIVATE KEY-----");
        return new PemReader(reader).ReadObject() switch
        {
            Ed25519PrivateKeyParameters key => key,
            _ => throw new InvalidKeyException("Not an Ed25519 private key."),
        };
    }

    /// <summary>RFC 7638 JWK thumbprint: SHA-256 over the required members in lexicographic order.</summary>
    internal static string Thumbprint(string x) =>
        Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes($"{{\"crv\":\"Ed25519\",\"kty\":\"OKP\",\"x\":\"{x}\"}}")));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

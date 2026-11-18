using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.OpenBadges.Configuration;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>
/// Makes and reads the id of a hosted MVP badge assertion. The id is
/// <c>base64url(utf8("{handle}:{year}") + mac)</c>, where <c>mac</c> is the first 16 bytes of an HMAC-SHA256 over the
/// same text with <see cref="OpenBadgesOptions.SigningKey"/>. It is stable (the same member and year always give the
/// same id), resolves back to handle and year without a database, and can't be made up without the key.
/// </summary>
public sealed class MvpBadgeTokenService
{
    private const int MacLength = 16;
    private const int MaxHandleLength = 39; // GitHub's own limit.
    private const string MacContext = "umbraco-mvp-badge:v1:";

    private readonly byte[]? _key;

    public MvpBadgeTokenService(IOptions<OpenBadgesOptions> options, ILogger<MvpBadgeTokenService> logger)
    {
        var key = options.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (key.Length < OpenBadgesOptions.MinSigningKeyLength)
        {
            // Never log the key itself.
            logger.LogError(
                "OpenBadges:SigningKey is shorter than {MinLength} characters; MVP badges are disabled",
                OpenBadgesOptions.MinSigningKeyLength);
            return;
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    /// <summary>False when no usable signing key is configured: then no badge can be issued or resolved.</summary>
    public bool IsConfigured => _key is not null;

    public string Create(string handle, int year)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("No OpenBadges:SigningKey configured.");
        }

        var payload = Payload(handle, year);
        return Base64Url.EncodeToString([.. payload, .. Mac(_key, payload)]);
    }

    /// <summary>Reads an id made by <see cref="Create"/>. False for anything malformed or signed with another key.</summary>
    public bool TryRead(string? token, out string handle, out int year)
    {
        handle = string.Empty;
        year = 0;

        if (_key is null || string.IsNullOrEmpty(token) || token.Length > 128 || !Base64Url.IsValid(token))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException)
        {
            return false;
        }

        // Only the canonical spelling: base64 can carry unused bits in its last character, and two ids for one
        // assertion would make the hosted id differ from the one baked into the image.
        if (bytes.Length <= MacLength || Base64Url.EncodeToString(bytes) != token)
        {
            return false;
        }

        var payload = bytes.AsSpan(0, bytes.Length - MacLength);
        var mac = bytes.AsSpan(bytes.Length - MacLength);
        if (!CryptographicOperations.FixedTimeEquals(mac, Mac(_key, payload)))
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(payload);
        var separator = text.LastIndexOf(':');
        if (separator <= 0
            || separator > MaxHandleLength
            || !int.TryParse(text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            year = 0;
            return false;
        }

        handle = text[..separator];
        return true;
    }

    private static byte[] Payload(string handle, int year) =>
        Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{handle}:{year}"));

    private static byte[] Mac(byte[] key, ReadOnlySpan<byte> payload)
    {
        byte[] message = [.. Encoding.UTF8.GetBytes(MacContext), .. payload];
        return HMACSHA256.HashData(key, message)[..MacLength];
    }
}

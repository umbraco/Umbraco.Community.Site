using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.OpenSsl;
using UmbracoCommunity.Web.Features.WebBotAuth;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.WebBotAuth;

public class KeyDirectoryServiceTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (KeyDirectoryService Service, Ed25519PublicKeyParameters Public) Create(string? pemOverride = null)
    {
        var gen = new Ed25519KeyPairGenerator();
        gen.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = gen.GenerateKeyPair();

        var sw = new StringWriter();
        new PemWriter(sw).WriteObject(pair.Private);

        var options = Options.Create(new WebBotAuthOptions { SigningPrivateKey = pemOverride ?? sw.ToString() });
        var service = new KeyDirectoryService(options, new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)), NullLogger<KeyDirectoryService>.Instance);
        return (service, (Ed25519PublicKeyParameters)pair.Public);
    }

    [Fact]
    public void Without_a_key_the_service_is_not_configured()
    {
        var (service, _) = Create(pemOverride: " ");
        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Bare_base64_body_without_pem_headers_is_accepted()
    {
        var gen = new Ed25519KeyPairGenerator();
        gen.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var sw = new StringWriter();
        new PemWriter(sw).WriteObject(gen.GenerateKeyPair().Private);
        var body = string.Concat(sw.ToString().Split('\n').Where(l => l.Length > 0 && !l.StartsWith("-----")));

        var (service, _) = Create(pemOverride: body);
        service.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void Garbage_key_disables_the_service_instead_of_throwing()
    {
        var (service, _) = Create(pemOverride: "not a key");
        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Directory_contains_only_public_members_and_a_thumbprint_kid()
    {
        var (service, pub) = Create();
        var signed = service.Sign("community.umbraco.com");

        using var doc = JsonDocument.Parse(signed.Body);
        var key = doc.RootElement.GetProperty("keys")[0];
        key.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("kty", "crv", "x", "kid");
        key.GetProperty("kty").GetString().Should().Be("OKP");
        key.GetProperty("crv").GetString().Should().Be("Ed25519");

        var x = Convert.ToBase64String(pub.GetEncoded()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        key.GetProperty("x").GetString().Should().Be(x);
        key.GetProperty("kid").GetString().Should().Be(KeyDirectoryService.Thumbprint(x));
    }

    [Fact]
    public void Signature_verifies_against_the_published_key_and_has_required_parameters()
    {
        var (service, pub) = Create();
        var signed = service.Sign("community.umbraco.com");

        signed.SignatureInput.Should().StartWith("sig1=(\"@authority\";req);alg=\"ed25519\";keyid=\"");
        signed.SignatureInput.Should().Contain("created=1800000000;expires=1800000300;tag=\"http-message-signatures-directory\"");

        var parameters = signed.SignatureInput["sig1=".Length..];
        var signatureBase = $"\"@authority\";req: community.umbraco.com\n\"@signature-params\": {parameters}";
        var sig = Convert.FromBase64String(signed.Signature["sig1=:".Length..^1]);

        var verifier = new Ed25519Signer();
        verifier.Init(false, pub);
        var data = Encoding.UTF8.GetBytes(signatureBase);
        verifier.BlockUpdate(data, 0, data.Length);
        verifier.VerifySignature(sig).Should().BeTrue();
    }
}

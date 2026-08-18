using FluentAssertions;
using Umbraco.Community.FormsSpamGuard.Tokens;
using Xunit;

namespace Umbraco.Community.FormsSpamGuard.Tests;

public class SpamGuardTokenServiceTests
{
    [Fact]
    public void Protect_then_unprotect_round_trips_the_payload()
    {
        SpamGuardTokenService service = TestHelpers.CreateTokenService();
        SpamGuardToken issued = service.Issue();

        service.TryUnprotect(service.Protect(issued), out SpamGuardToken? result).Should().BeTrue();

        result!.DecoyFieldName.Should().Be(issued.DecoyFieldName);
        result.Nonce.Should().Be(issued.Nonce);
        // Serialised to whole seconds, so compare at that resolution.
        result.RenderedUtc.ToUnixTimeSeconds().Should().Be(issued.RenderedUtc.ToUnixTimeSeconds());
    }

    [Fact]
    public void Issue_randomises_the_decoy_name_on_every_render()
    {
        SpamGuardTokenService service = TestHelpers.CreateTokenService();

        var names = Enumerable.Range(0, 25).Select(_ => service.Issue().DecoyFieldName).ToList();

        // This is the property the whole design rests on: a scraper cannot learn the decoy name once and reuse
        // it. Twenty-five draws from ten candidates each with a random suffix should never repeat.
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Issue_produces_a_decoy_name_that_does_not_look_like_the_built_in_honeypot()
    {
        SpamGuardTokenService service = TestHelpers.CreateTokenService();

        // The built-in Forms honeypot is named with the form GUID stripped of dashes, i.e. exactly 32 hex
        // characters, which is what makes it trivially skippable. Ours must never match that shape.
        service.Issue().DecoyFieldName.Should().NotMatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void Decoy_names_are_all_inert_to_browser_autofill()
    {
        // The decoy's name is as strong an autofill signal as its label. Anything in the autofill vocabulary
        // risks a real visitor's browser completing the decoy and their enquiry being silently rejected, which
        // costs far more than letting one bot through.
        SpamGuardTokenService service = TestHelpers.CreateTokenService();

        var names = Enumerable.Range(0, 200)
            .Select(_ => service.Issue().DecoyFieldName.ToLowerInvariant())
            .Distinct()
            .ToList();

        foreach (var token in new[]
                 {
                     "email", "name", "phone", "tel", "address", "city", "zip", "postal",
                     "country", "company", "organi", "url", "website", "homepage", "fax",
                     "card", "username", "password",
                 })
        {
            names.Should().NotContain(n => n.Contains(token), $"'{token}' is browser autofill vocabulary");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-protected-value")]
    public void TryUnprotect_rejects_absent_and_malformed_values(string? value)
    {
        SpamGuardTokenService service = TestHelpers.CreateTokenService();

        service.TryUnprotect(value, out SpamGuardToken? result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryUnprotect_rejects_a_tampered_value()
    {
        SpamGuardTokenService service = TestHelpers.CreateTokenService();
        var protectedValue = service.Protect(service.Issue());

        var tampered = protectedValue[..^4] + "AAAA";

        service.TryUnprotect(tampered, out SpamGuardToken? result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryUnprotect_rejects_a_truncated_value()
    {
        SpamGuardTokenService service = TestHelpers.CreateTokenService();
        var protectedValue = service.Protect(service.Issue());

        service.TryUnprotect(protectedValue[..(protectedValue.Length / 2)], out _).Should().BeFalse();
    }

    [Fact]
    public void TryUnprotect_rejects_a_token_protected_by_a_different_provider()
    {
        // Two providers stand in for two installations, or one installation whose purpose string changed.
        var protectedValue = TestHelpers.CreateTokenService().Protect(TestHelpers.CreateToken());

        TestHelpers.CreateTokenService().TryUnprotect(protectedValue, out _).Should().BeFalse();
    }

    [Fact]
    public void ComputeJavaScriptAnswer_matches_the_shipped_script()
    {
        SpamGuardTokenService service = TestHelpers.CreateTokenService();

        // Expected value derived by hand from the documented algorithm (reverse, base64, url-safe, unpadded).
        // spam-guard.js must produce the same string for the same nonce; if this assertion changes, that file
        // has to change with it.
        service.ComputeJavaScriptAnswer("abc123").Should().Be("MzIxY2Jh");
    }

    [Fact]
    public void ComputeJavaScriptAnswer_returns_empty_for_an_empty_nonce()
    {
        TestHelpers.CreateTokenService().ComputeJavaScriptAnswer(string.Empty).Should().BeEmpty();
    }
}

using FluentAssertions;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.OpenBadges;

public class MvpBadgeTokenServiceTests
{
    [Fact]
    public void A_token_reads_back_to_its_handle_and_year()
    {
        var tokens = OpenBadgesTestData.Tokens();

        var token = tokens.Create("Octo-Cat", 2026);

        tokens.TryRead(token, out var handle, out var year).Should().BeTrue();
        handle.Should().Be("Octo-Cat");
        year.Should().Be(2026);
    }

    [Fact]
    public void A_token_is_stable_and_url_safe()
    {
        var tokens = OpenBadgesTestData.Tokens();

        var token = tokens.Create("octocat", 2026);

        tokens.Create("octocat", 2026).Should().Be(token);
        token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void A_token_for_another_year_or_handle_differs()
    {
        var tokens = OpenBadgesTestData.Tokens();

        tokens.Create("octocat", 2025).Should().NotBe(tokens.Create("octocat", 2026));
        tokens.Create("octodog", 2026).Should().NotBe(tokens.Create("octocat", 2026));
    }

    [Fact]
    public void A_tampered_token_is_rejected()
    {
        var tokens = OpenBadgesTestData.Tokens();
        var token = tokens.Create("octocat", 2026);

        foreach (var index in new[] { 0, token.Length / 2, token.Length - 1 })
        {
            var swapped = token[index] == 'A' ? 'B' : 'A';
            var tampered = token[..index] + swapped + token[(index + 1)..];

            tokens.TryRead(tampered, out _, out _).Should().BeFalse($"character {index} was changed");
        }
    }

    [Fact]
    public void A_token_made_up_without_the_key_is_rejected()
    {
        var forged = System.Buffers.Text.Base64Url.EncodeToString(
            [.. System.Text.Encoding.UTF8.GetBytes("octocat:2026"), .. new byte[16]]);

        OpenBadgesTestData.Tokens().TryRead(forged, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void A_token_signed_with_another_key_is_rejected()
    {
        var token = OpenBadgesTestData.Tokens("another-signing-key-that-is-long-enough-987654").Create("octocat", 2026);

        OpenBadgesTestData.Tokens().TryRead(token, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void Garbage_is_rejected(string? token) =>
        OpenBadgesTestData.Tokens().TryRead(token, out _, out _).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    public void Without_a_usable_key_the_feature_is_off(string? key)
    {
        var tokens = OpenBadgesTestData.Tokens(key);

        tokens.IsConfigured.Should().BeFalse();
        tokens.TryRead(OpenBadgesTestData.Tokens().Create("octocat", 2026), out _, out _).Should().BeFalse();
    }
}

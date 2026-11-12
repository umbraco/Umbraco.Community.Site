using UmbracoCommunity.SearchTracker.Recording;

namespace UmbracoCommunity.SearchTracker.Tests;

public class SearchTermNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t ")]
    public void Returns_null_for_empty_input(string? raw)
        => SearchTermNormalizer.Normalize(raw, 100).Should().BeNull();

    [Fact]
    public void Lower_cases_and_collapses_whitespace()
        => SearchTermNormalizer.Normalize("  Block   GRID\tEditor ", 100).Should().Be("block grid editor");

    [Fact]
    public void Masks_email_addresses()
        => SearchTermNormalizer.Normalize("contact Jane.Doe@example.com please", 100).Should().Be("contact [email] please");

    [Theory]
    [InlineData("call +45 12 34 56 78", "call [number]")]
    [InlineData("call 12 34 56 78", "call [number]")]
    [InlineData("call (+45) 1234 5678", "call [number]")]
    [InlineData("order 12345678", "order [number]")]
    public void Masks_long_numbers(string raw, string expected)
        => SearchTermNormalizer.Normalize(raw, 100).Should().Be(expected);

    [Theory]
    [InlineData("umbraco 13.2.1", "umbraco 13.2.1")]
    [InlineData("codegarden 2026", "codegarden 2026")]
    [InlineData("codegarden 2026 2027", "codegarden 2026 2027")]
    [InlineData("upgrade 14.0.1 to 16.2.0", "upgrade 14.0.1 to 16.2.0")]
    [InlineData("v13 v14 v15", "v13 v14 v15")]
    public void Keeps_short_numbers_and_versions(string raw, string expected)
        => SearchTermNormalizer.Normalize(raw, 100).Should().Be(expected);

    [Fact]
    public void Truncates_to_max_length()
        => SearchTermNormalizer.Normalize("abcdefghij klm", 11).Should().Be("abcdefghij");

    [Fact]
    public void Replaces_control_characters()
        => SearchTermNormalizer.Normalize("a\u0000b", 100).Should().Be("a b");
}

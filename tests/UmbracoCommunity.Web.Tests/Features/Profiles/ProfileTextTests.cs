using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class ProfileTextTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  \t\n ", null)]
    [InlineData("  hello  ", "hello")]
    public void Normalize_trims_and_turns_blank_text_into_null(string? input, string? expected)
    {
        ProfileText.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeAndTruncate_leaves_short_text_alone()
    {
        ProfileText.NormalizeAndTruncate(" short ", 10).Should().Be("short");
    }

    [Fact]
    public void NormalizeAndTruncate_cuts_long_text_to_the_limit()
    {
        ProfileText.NormalizeAndTruncate("abcdefghij", 4).Should().Be("abcd");
    }

    [Fact]
    public void NormalizeAndTruncate_does_not_split_a_surrogate_pair()
    {
        // "ab" followed by an emoji made of two UTF-16 code units.
        var text = "ab\U0001F600cd";

        ProfileText.NormalizeAndTruncate(text, 3).Should().Be("ab");
    }
}

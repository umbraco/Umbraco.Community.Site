using FluentAssertions;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Tests.Utilities;

public class StringUtilitiesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void RandomString_ReturnsStringOfCorrectLength(int length)
    {
        var result = StringUtilities.RandomString(length);

        result.Should().HaveLength(length);
    }

    [Fact]
    public void RandomString_ReturnsOnlyAlphabeticCharacters()
    {
        var result = StringUtilities.RandomString(100);

        result.Should().MatchRegex("^[A-Za-z]*$");
    }

    [Fact]
    public void RandomString_ReturnsDifferentValuesOnSubsequentCalls()
    {
        var result1 = StringUtilities.RandomString(20);
        var result2 = StringUtilities.RandomString(20);

        // While theoretically they could be the same, it's astronomically unlikely
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void RandomString_WithZeroLength_ReturnsEmptyString()
    {
        var result = StringUtilities.RandomString(0);

        result.Should().BeEmpty();
    }
}

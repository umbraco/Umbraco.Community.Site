using FluentAssertions;
using UmbracoCommunity.Web.Helpers;

namespace UmbracoCommunity.Tests.Helpers;

public class ColourHelperTests
{
    [Theory]
    [InlineData("#FF0000", "#FF0000")]
    [InlineData("FF0000", "#FF0000")]
    [InlineData("#fff", "#fff")]
    [InlineData("fff", "#fff")]
    public void GetOptionalColor_ReturnsFormattedColor(string input, string expected)
    {
        var result = input.GetOptionalColor();

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetOptionalColor_ReturnsNullForEmptyInput(string? input)
    {
        var result = input.GetOptionalColor();

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("#FF0000", "#FF0000")]
    [InlineData("FF0000", "#FF0000")]
    public void GetRequiredColor_ReturnsFormattedColor(string input, string expected)
    {
        var result = input.GetRequiredColor("#000000");

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "#default")]
    [InlineData("", "#default")]
    [InlineData("#", "#default")]
    public void GetRequiredColor_ReturnsDefaultForInvalidInput(string? input, string expected)
    {
        var result = input.GetRequiredColor("#default");

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("#3544B1", true)]
    [InlineData("#1b264f", true)]
    [InlineData("#3544b1", true)]  // Case insensitive
    [InlineData("#1B264F", true)]  // Case insensitive
    [InlineData("#ffffff", false)]
    [InlineData("#FF0000", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsDark_String_ReturnsExpectedResult(string? colour, bool expected)
    {
        var result = colour.IsDark();

        result.Should().Be(expected);
    }
}

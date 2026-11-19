using FluentAssertions;
using UmbracoCommunity.Web.Helpers;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Helpers;

public class ColourHelperTests
{
    [Fact]
    public void Black_on_white_is_the_maximum_contrast()
    {
        ColourHelper.ContrastRatio("#000000", "#ffffff").Should().BeApproximately(21, 0.01);
    }

    [Theory]
    [InlineData("#f2ebe6", "#F2EBE6")]
    [InlineData("f9f7f4", "#f9f7f4")]
    public void Identical_colours_have_no_contrast(string first, string second)
    {
        ColourHelper.ContrastRatio(first, second).Should().BeApproximately(1, 0.0001);
    }

    [Fact]
    public void White_on_the_palette_off_white_is_barely_distinguishable()
    {
        ColourHelper.ContrastRatio("#ffffff", "#f9f7f4").Should().BeLessThan(1.1);
    }

    [Theory]
    [InlineData(null, "#ffffff")]
    [InlineData("not a colour", "#ffffff")]
    [InlineData("#fff", "#ffffff")]
    public void Returns_null_for_anything_that_is_not_a_six_digit_hex_colour(string? first, string second)
    {
        ColourHelper.ContrastRatio(first, second).Should().BeNull();
    }
}

using FluentAssertions;
using Umbraco.Cms.Core.Strings;
using UmbracoCommunity.Web.Extensions;

namespace UmbracoCommunity.Tests.Extensions;

public class HtmlHelperExtensionsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("<p></p>", true)]
    [InlineData("<p>   </p>", true)]
    [InlineData("<div><span></span></div>", true)]
    [InlineData("<p>Hello</p>", false)]
    [InlineData("Plain text", false)]
    [InlineData("<div>Content</div>", false)]
    public void IsEmptyHtmlString_ReturnsExpectedResult(string? html, bool expected)
    {
        IHtmlEncodedString? htmlEncodedString = html != null ? new HtmlEncodedString(html) : null;

        var result = htmlEncodedString.IsEmptyHtmlString();

        result.Should().Be(expected);
    }

    [Fact]
    public void ReplaceLineBreaks_ReplacesNewlinesWithBrTags()
    {
        var text = "Line 1\nLine 2\nLine 3";

        var result = HtmlHelperExtensions.ReplaceLineBreaks(text);

        result.ToString().Should().Be("Line 1<br />Line 2<br />Line 3");
    }

    [Fact]
    public void ReplaceLineBreaks_ReplacesCarriageReturnNewlines()
    {
        var text = "Line 1\r\nLine 2\r\nLine 3";

        var result = HtmlHelperExtensions.ReplaceLineBreaks(text);

        result.ToString().Should().Be("Line 1<br />Line 2<br />Line 3");
    }

    [Fact]
    public void ReplaceLineBreaks_ReplacesCarriageReturns()
    {
        var text = "Line 1\rLine 2\rLine 3";

        var result = HtmlHelperExtensions.ReplaceLineBreaks(text);

        result.ToString().Should().Be("Line 1<br />Line 2<br />Line 3");
    }

    [Fact]
    public void ReplaceLineBreaks_HtmlEncodesSpecialCharacters()
    {
        var text = "<script>alert('xss')</script>";

        var result = HtmlHelperExtensions.ReplaceLineBreaks(text);

        result.ToString().Should().NotContain("<script>");
        result.ToString().Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void ReplaceLineBreaks_HandlesEmptyString()
    {
        var result = HtmlHelperExtensions.ReplaceLineBreaks("");

        result.ToString().Should().BeEmpty();
    }

    [Fact]
    public void ReplaceLineBreaks_HandlesMixedLineEndings()
    {
        var text = "Line 1\r\nLine 2\nLine 3\rLine 4";

        var result = HtmlHelperExtensions.ReplaceLineBreaks(text);

        result.ToString().Should().Be("Line 1<br />Line 2<br />Line 3<br />Line 4");
    }
}

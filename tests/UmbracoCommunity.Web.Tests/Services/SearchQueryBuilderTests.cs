using FluentAssertions;
using UmbracoCommunity.Web.Services;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Services;

public class SearchQueryBuilderTests
{
    private static readonly string[] Fields = ["nodeName", "bodyText"];
    private static readonly string[] FuzzyFields = ["nodeName"];

    [Fact]
    public void Terms_DropsFillerWordsAndPunctuation()
    {
        SearchQueryBuilder.Terms("Who is SPEAKING at Codegarden?!").Should().Equal("speaking", "codegarden");
    }

    [Fact]
    public void Terms_FoldsAccents()
    {
        SearchQueryBuilder.Terms("Café Ångström København Straße Ærø").Should().Equal("cafe", "angstrom", "kobenhavn", "strasse", "aero");
    }

    [Theory]
    [InlineData("speaking", "speak")]
    [InlineData("speakers", "speak")]
    [InlineData("speaker", "speak")]
    [InlineData("tickets", "ticket")]
    [InlineData("garder", "gard")]
    [InlineData("forms", "form")]
    [InlineData("news", "news")]
    [InlineData("umbraco", "umbraco")]
    public void Root_StripsCommonEndings(string term, string expected)
    {
        SearchQueryBuilder.Root(term).Should().Be(expected);
    }

    [Fact]
    public void Build_MatchesExactRootAndTyposWithTitleWeighting_TyposOnlyOnFuzzyFields()
    {
        var query = SearchQueryBuilder.Build("who is speaking", Fields, "nodeName", FuzzyFields);

        query.Should().Be(
            "nodeName:speaking^9 nodeName:speak*^4.5 nodeName:speaking~2^1.5 " +
            "bodyText:speaking^3 bodyText:speak*^1.5");
    }

    [Fact]
    public void Build_SkipsFuzzyForShortWords()
    {
        SearchQueryBuilder.Build("mvp", Fields, "nodeName", FuzzyFields).Should().NotContain("~");
    }

    [Theory]
    [InlineData("")]
    [InlineData("who is the")]
    [InlineData("?!")]
    public void Build_ReturnsNullWithoutSearchableWords(string query)
    {
        SearchQueryBuilder.Build(query, Fields, "nodeName", FuzzyFields).Should().BeNull();
    }
}

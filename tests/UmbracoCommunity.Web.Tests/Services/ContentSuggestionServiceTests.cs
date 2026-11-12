using FluentAssertions;
using UmbracoCommunity.Web.Services;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Services;

public class ContentSuggestionServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t ")]
    public void NormaliseQuery_Empty_ReturnsNull(string? query)
    {
        ContentSuggestionService.NormaliseQuery(query).Should().BeNull();
    }

    [Fact]
    public void NormaliseQuery_CollapsesWhitespaceAndLowerCases()
    {
        ContentSuggestionService.NormaliseQuery("  How much   is a\tTICKET ").Should().Be("how much is a ticket");
    }

    [Fact]
    public void NormaliseQuery_CapsLength()
    {
        var query = new string('a', ContentSuggestionService.MaxQueryLength + 50);

        ContentSuggestionService.NormaliseQuery(query).Should().HaveLength(ContentSuggestionService.MaxQueryLength);
    }

    [Fact]
    public void SelectSuggestionIds_KeepsSearchablePagesOnThisSiteInOrder()
    {
        var candidates = new[]
        {
            new ContentSuggestionService.SuggestionCandidate(10, 1, true),
            new ContentSuggestionService.SuggestionCandidate(20, 2, true),
            new ContentSuggestionService.SuggestionCandidate(30, 1, false),
            new ContentSuggestionService.SuggestionCandidate(40, 1, true),
            new ContentSuggestionService.SuggestionCandidate(10, 1, true),
            new ContentSuggestionService.SuggestionCandidate(50, 1, true),
        };

        ContentSuggestionService.SelectSuggestionIds(candidates, tenantRootId: 1, count: 2).Should().Equal(10, 40);
    }
}

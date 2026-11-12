using FluentAssertions;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Controllers.Render;
using UmbracoCommunity.Web.Models.Pages;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Controllers.Render;

public class SearchPageControllerTests
{
    private static IPublishedContent Page(Guid key) => Mock.Of<IPublishedContent>(p => p.Key == key);

    private static SearchResultItem Result(Guid? key) => new() { Name = "Result", Url = "/result/", ContentKey = key };

    [Fact]
    public void SelectRelated_SkipsPagesAlreadyInTheResults()
    {
        var shown = Guid.NewGuid();
        var other = Guid.NewGuid();

        var related = SearchPageController.SelectRelated([Page(shown), Page(other)], [Result(shown), Result(null)], max: 3);

        related.Select(p => p.Key).Should().Equal(other);
    }

    [Fact]
    public void SelectRelated_TakesAtMostMax()
    {
        var suggestions = Enumerable.Range(0, 5).Select(_ => Page(Guid.NewGuid())).ToList();

        SearchPageController.SelectRelated(suggestions, [], max: 3).Should().HaveCount(3);
    }
}

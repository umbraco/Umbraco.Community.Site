using FluentAssertions;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.ViewModelBuilders.Pages;

namespace UmbracoCommunity.Tests.ViewModelBuilders;

/// <summary>
/// Tests for simple page ViewModelBuilders that have minimal logic.
/// Note: HomePageViewModelBuilder and ContentPageViewModelBuilder use As&lt;T&gt;() extension
/// which requires actual Umbraco content types. Testing those would require integration tests
/// or extensive mocking of Umbraco's model generation.
/// </summary>
public class SimplePageViewModelBuilderTests
{
    #region BlogPageViewModelBuilder Tests

    [Fact]
    public void BlogPageViewModelBuilder_Build_ReturnsViewModelWithCurrentPage()
    {
        var builder = new BlogPageViewModelBuilder();
        var mockPage = CreateMockPublishedContent("blog", "Blog");

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.Should().NotBeNull();
        result.ContentTypeAlias.Should().Be("blog");
    }

    [Fact]
    public void BlogPageViewModelBuilder_Build_SetsNameFromPage()
    {
        var builder = new BlogPageViewModelBuilder();
        var mockPage = CreateMockPublishedContent("blog", "Community Blog");

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.Name.Should().Be("Community Blog");
    }

    #endregion

    #region ArticlePageViewModelBuilder Tests

    [Fact]
    public void ArticlePageViewModelBuilder_Build_ReturnsViewModelWithCurrentPage()
    {
        var builder = new ArticlePageViewModelBuilder();
        var mockPage = CreateMockPublishedContent("article", "Test Article");

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.Should().NotBeNull();
        result.ContentTypeAlias.Should().Be("article");
    }

    [Fact]
    public void ArticlePageViewModelBuilder_Build_SetsNameFromPage()
    {
        var builder = new ArticlePageViewModelBuilder();
        var mockPage = CreateMockPublishedContent("article", "My Article Title");

        var result = builder.Build(mockPage.Object, Mock.Of<IUmbracoContext>());

        result.Name.Should().Be("My Article Title");
    }

    #endregion

    #region Helper Methods

    private static Mock<IPublishedContent> CreateMockPublishedContent(string contentTypeAlias, string name)
    {
        var mockContentType = new Mock<IPublishedContentType>();
        mockContentType.Setup(x => x.Alias).Returns(contentTypeAlias);

        var mockContent = new Mock<IPublishedContent>();
        mockContent.Setup(x => x.ContentType).Returns(mockContentType.Object);
        mockContent.Setup(x => x.Id).Returns(1);
        mockContent.Setup(x => x.Name).Returns(name);
        mockContent.Setup(x => x.Key).Returns(Guid.NewGuid());
        mockContent.Setup(x => x.UrlSegment).Returns(name.ToLowerInvariant().Replace(" ", "-"));
        mockContent.Setup(x => x.Level).Returns(2);
        mockContent.Setup(x => x.SortOrder).Returns(0);
        mockContent.Setup(x => x.CreateDate).Returns(DateTime.UtcNow.AddDays(-30));
        mockContent.Setup(x => x.UpdateDate).Returns(DateTime.UtcNow);

        return mockContent;
    }

    #endregion
}

/// <summary>
/// Note on HomePageViewModelBuilder and ContentPageViewModelBuilder:
///
/// These builders use the As&lt;T&gt;() extension method to cast IPublishedContent to
/// specific generated models (Home, ContentPage). This requires:
///
/// 1. The content to implement the specific model interface
/// 2. Umbraco's ModelsBuilder-generated types
/// 3. IPublishedValueFallback for property access
///
/// Testing these would require either:
/// - Integration tests with a real Umbraco context
/// - Complex mocking of the entire Umbraco model infrastructure
///
/// The logic in these builders is minimal (ParseBlockGrid and property mapping),
/// so the value of such tests would be low compared to the complexity.
///
/// For comprehensive testing of page rendering, consider integration tests
/// using WebApplicationFactory with a test database.
/// </summary>
public class HomePageViewModelBuilderDocumentation
{
    // This class exists to document why certain builders aren't unit tested.
    // See summary above for explanation.
}

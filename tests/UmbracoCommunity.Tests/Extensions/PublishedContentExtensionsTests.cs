using FluentAssertions;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Tests.Extensions;

/// <summary>
/// Tests for PublishedContentExtensions.
/// Note: Tests for GetSiteSettings, GetNavigationSettings, and GetSocialSettings require
/// mocking Umbraco's Root() extension method which isn't possible with Moq.
/// Those methods would need integration tests with a real Umbraco context.
/// </summary>
public class PublishedContentExtensionsTests
{
    private readonly Mock<IPublishedValueFallback> _publishedValueFallback = new();

    [Fact]
    public void As_WithValidContent_ReturnsTypedContent()
    {
        var mockContent = CreateMockPublishedContent("settings");
        var settings = new Settings(mockContent.Object, _publishedValueFallback.Object);

        var result = settings.As<Settings>();

        result.Should().NotBeNull();
        result.Should().BeSameAs(settings);
    }

    [Fact]
    public void As_WithNullContent_ThrowsArgumentException()
    {
        IPublishedContent? nullContent = null;

        var act = () => nullContent.As<Settings>();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*null*");
    }

    [Fact]
    public void As_WithWrongContentType_ThrowsArgumentException()
    {
        var mockContent = CreateMockPublishedContent("homepage");

        var act = () => mockContent.Object.As<Settings>();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Settings*");
    }

    [Fact]
    public void As_ExceptionMessage_ContainsActualContentTypeAlias()
    {
        var mockContent = CreateMockPublishedContent("myContentType");

        var act = () => mockContent.Object.As<Settings>();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*myContentType*");
    }

    [Fact]
    public void As_WithDerivedType_WorksCorrectly()
    {
        var mockContent = CreateMockPublishedContent("settings");
        var settings = new Settings(mockContent.Object, _publishedValueFallback.Object);

        // Should be able to cast to base interface
        var result = settings.As<IPublishedContent>();

        result.Should().NotBeNull();
    }

    private Mock<IPublishedContent> CreateMockPublishedContent(string contentTypeAlias)
    {
        var mockContentType = new Mock<IPublishedContentType>();
        mockContentType.Setup(x => x.Alias).Returns(contentTypeAlias);

        var mockContent = new Mock<IPublishedContent>();
        mockContent.Setup(x => x.ContentType).Returns(mockContentType.Object);

        return mockContent;
    }
}

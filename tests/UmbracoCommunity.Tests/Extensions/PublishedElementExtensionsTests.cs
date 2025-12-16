using FluentAssertions;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Extensions;

namespace UmbracoCommunity.Tests.Extensions;

public class PublishedElementExtensionsTests
{
    [Fact]
    public void As_WithValidElement_ReturnsTypedElement()
    {
        var mockElement = CreateMockPublishedElement("testAlias");

        // Since we're testing the cast itself, we need an actual implementation
        // The mock implements IPublishedElement, so casting to IPublishedElement should work
        var act = () => mockElement.Object.As<IPublishedElement>();

        act.Should().NotThrow();
    }

    [Fact]
    public void As_WithNullElement_ThrowsArgumentException()
    {
        IPublishedElement? nullElement = null;

        var act = () => nullElement.As<IPublishedElement>();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*null*");
    }

    [Fact]
    public void As_WithWrongElementType_ThrowsArgumentException()
    {
        var mockElement = CreateMockPublishedElement("wrongType");

        // Try to cast to a specific type that the mock doesn't implement
        var act = () => mockElement.Object.As<ISpecificPublishedElement>();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ISpecificPublishedElement*");
    }

    [Fact]
    public void As_ExceptionMessage_ContainsContentTypeAlias()
    {
        var mockElement = CreateMockPublishedElement("myContentType");

        var act = () => mockElement.Object.As<ISpecificPublishedElement>();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*myContentType*");
    }

    private Mock<IPublishedElement> CreateMockPublishedElement(string contentTypeAlias)
    {
        var mockContentType = new Mock<IPublishedContentType>();
        mockContentType.Setup(x => x.Alias).Returns(contentTypeAlias);

        var mockElement = new Mock<IPublishedElement>();
        mockElement.Setup(x => x.ContentType).Returns(mockContentType.Object);

        return mockElement;
    }

    // Marker interface for testing type mismatch
    private interface ISpecificPublishedElement : IPublishedElement { }
}

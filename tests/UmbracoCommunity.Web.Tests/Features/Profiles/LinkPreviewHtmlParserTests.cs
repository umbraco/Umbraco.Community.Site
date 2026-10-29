using FluentAssertions;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class LinkPreviewHtmlParserTests
{
    private static readonly Uri Page = new("https://example.org/blog/post");

    [Fact]
    public void Reads_the_share_image_title_and_icon_resolving_relative_addresses()
    {
        const string html = """
            <html><head>
            <title>Fallback title</title>
            <meta property="og:title" content="Shared &amp; titled">
            <meta property="og:image" content="/img/cover.png">
            <link rel="shortcut icon" href="/icons/site.png">
            </head><body></body></html>
            """;

        var preview = LinkPreviewHtmlParser.Parse(html, Page);

        preview.Status.Should().Be(LinkPreviewStatus.Available);
        preview.Title.Should().Be("Shared & titled");
        preview.ImageUrl.Should().Be("https://example.org/img/cover.png");
        preview.FaviconUrl.Should().Be("https://example.org/icons/site.png");
    }

    [Fact]
    public void A_page_without_a_share_image_still_gets_its_favicon()
    {
        var preview = LinkPreviewHtmlParser.Parse("<html><head><title>Plain</title></head></html>", Page);

        preview.ImageUrl.Should().BeNull();
        preview.Title.Should().Be("Plain");
        preview.FaviconUrl.Should().Be("https://example.org/favicon.ico");
    }

    [Fact]
    public void Images_that_are_not_https_are_dropped()
    {
        const string html = """<head><meta property="og:image" content="http://example.org/a.png"><link rel="icon" href="data:image/png;base64,AAAA"></head>""";

        var preview = LinkPreviewHtmlParser.Parse(html, Page);

        preview.ImageUrl.Should().BeNull();
        preview.FaviconUrl.Should().Be("https://example.org/favicon.ico");
    }
}

using FluentAssertions;
using UmbracoCommunity.Web.Services;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Services;

public class PageNotFoundSuggestionServiceTests
{
    [Theory]
    // Classic scanner probes (highest volume seen in the live 404 storm).
    [InlineData("/wp-login.php")]
    [InlineData("/xmlrpc.php")]
    [InlineData("/index.php5")]
    [InlineData("/admin.aspx")]
    [InlineData("/shell.jsp")]
    [InlineData("/config.json")]
    [InlineData("/sitemap.xml")]
    [InlineData("/dump.sql")]
    [InlineData("/site.tar.gz")]
    // Dotfiles: the leading dot is the extension itself.
    [InlineData("/.env")]
    [InlineData("/some/path/.env")]
    public void HasNonContentExtension_NonContentPaths_ReturnsTrue(string path)
    {
        PageNotFoundSuggestionService.HasNonContentExtension(path).Should().BeTrue();
    }

    [Theory]
    // Real (extensionless) Umbraco content URLs — including mistyped ones we DO want to suggest for.
    [InlineData("/blog/my-article")]
    [InlineData("/learn-about-the-community/badges/")]
    [InlineData("/the-community-blog/the-umbraco-forum-is-moving")]
    [InlineData("/")]
    [InlineData("")]
    // Extensions not on the block list must not be filtered (avoid false positives).
    [InlineData("/.well-known/security.txt")]
    [InlineData("/page.html")]
    // .zip is a legitimate URL shape here — /seed/latest.zip is a real public endpoint.
    [InlineData("/seed/latest.zip")]
    // A trailing dot has no usable extension.
    [InlineData("/weird.")]
    public void HasNonContentExtension_ContentPaths_ReturnsFalse(string path)
    {
        PageNotFoundSuggestionService.HasNonContentExtension(path).Should().BeFalse();
    }
}

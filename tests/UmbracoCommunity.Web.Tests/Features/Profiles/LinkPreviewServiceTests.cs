using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Profiles.LinkPreviews;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

public class LinkPreviewServiceTests
{
    // Address literals need no DNS lookup, so these tests never touch the network.
    private const string PublicUrl = "https://93.184.216.34/";

    private static (LinkPreviewService Service, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var client = new LinkPreviewHttpClient(new HttpClient(handler));
        return (new LinkPreviewService(client, NullLogger<LinkPreviewService>.Instance), handler);
    }

    private static HttpResponseMessage Html(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    [Theory]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://10.0.0.5/")]
    [InlineData("https://192.168.1.10/")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://[::1]/")]
    [InlineData("http://93.184.216.34/")]
    public async Task Addresses_that_are_private_or_not_https_are_refused_without_a_request(string url)
    {
        var (service, handler) = Create(_ => Html("<head></head>"));

        var preview = await service.FetchAsync(url, CancellationToken.None);

        preview.Status.Should().Be(LinkPreviewStatus.Unreachable);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_page_with_no_share_image_falls_back_to_its_favicon()
    {
        var (service, _) = Create(_ => Html("<head><title>Hi</title></head>"));

        var preview = await service.FetchAsync(PublicUrl, CancellationToken.None);

        preview.Status.Should().Be(LinkPreviewStatus.Available);
        preview.ImageUrl.Should().BeNull();
        preview.FaviconUrl.Should().Be("https://93.184.216.34/favicon.ico");
    }

    [Theory]
    [InlineData("https://10.0.0.5/inside")]
    [InlineData("http://93.184.216.34/plain")]
    public async Task A_redirect_to_a_blocked_address_is_not_followed(string target)
    {
        var (service, handler) = Create(request =>
            request.RequestUri!.Host == "93.184.216.34" && request.RequestUri.Scheme == "https"
                ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri(target) } }
                : Html("<head></head>"));

        var preview = await service.FetchAsync(PublicUrl, CancellationToken.None);

        preview.Status.Should().Be(LinkPreviewStatus.Unreachable);
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_redirect_to_another_public_https_address_is_followed()
    {
        var (service, handler) = Create(request => request.RequestUri!.AbsolutePath == "/"
            ? new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri("/home", UriKind.Relative) } }
            : Html("""<head><meta property="og:image" content="/c.png"></head>"""));

        var preview = await service.FetchAsync(PublicUrl, CancellationToken.None);

        preview.ImageUrl.Should().Be("https://93.184.216.34/c.png");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Pages_that_are_not_html_or_that_fail_are_unreachable()
    {
        var (notHtml, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        var (failing, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        (await notHtml.FetchAsync(PublicUrl, CancellationToken.None)).Status.Should().Be(LinkPreviewStatus.Unreachable);
        (await failing.FetchAsync(PublicUrl, CancellationToken.None)).Status.Should().Be(LinkPreviewStatus.Unreachable);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }
}

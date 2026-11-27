using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs.Controllers;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Feeds.CommunityBlogs;

public class FeedSubmissionImageProxyServiceTests
{
    private static FeedSubmissionImageProxyService Create() => new(
        new FeedSubmissionImageProxyHttpClient(new HttpClient()),
        new MemoryCache(new MemoryCacheOptions()),
        new EphemeralDataProtectionProvider(),
        NullLogger<FeedSubmissionImageProxyService>.Instance);

    private static string TokenOf(string? proxyUrl) => Uri.UnescapeDataString(proxyUrl!.Split("token=")[1]);

    [Fact]
    public async Task A_tampered_or_foreign_token_is_refused()
    {
        var service = Create();
        var token = TokenOf(service.CreateProxyUrl("https://example.org/a.png"));

        (await service.FetchAsync(token + "x", CancellationToken.None)).Should().BeNull();
        (await service.FetchAsync("not-a-token", CancellationToken.None)).Should().BeNull();
        (await Create().FetchAsync(token, CancellationToken.None)).Should().BeNull("a token from another key ring is not ours");
    }

    [Theory]
    [InlineData("https://127.0.0.1/a.png")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("ftp://example.org/a.png")]
    public async Task A_valid_token_for_a_private_or_non_web_address_is_still_refused(string url)
    {
        var service = Create();

        (await service.FetchAsync(TokenOf(service.CreateProxyUrl(url)), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void The_same_image_gets_the_same_url_so_the_browser_can_cache_it()
    {
        // Each reload of a member's links (after every reorder) used to give each icon a new URL, so the browser
        // fetched every icon again until the rate limit answered 429 and the icons went blank.
        var service = Create();

        var first = service.CreateProxyUrl("https://example.org/favicon.ico");
        var again = service.CreateProxyUrl("https://example.org/favicon.ico");
        var other = service.CreateProxyUrl("https://example.org/other.ico");

        again.Should().Be(first);
        other.Should().NotBe(first);
    }

    [Fact]
    public void A_reused_url_still_carries_a_valid_token()
    {
        var protector = new EphemeralDataProtectionProvider();
        var service = new FeedSubmissionImageProxyService(
            new FeedSubmissionImageProxyHttpClient(new HttpClient()),
            new MemoryCache(new MemoryCacheOptions()),
            protector,
            NullLogger<FeedSubmissionImageProxyService>.Instance);
        service.CreateProxyUrl("https://example.org/favicon.ico");

        var token = TokenOf(service.CreateProxyUrl("https://example.org/favicon.ico"));

        protector.CreateProtector("UmbracoCommunity.FeedSubmissionImageProxy.v1").ToTimeLimitedDataProtector()
            .Unprotect(token).Should().Be("https://example.org/favicon.ico");
    }

    [Fact]
    public void The_image_proxy_has_its_own_rate_limit_rather_than_the_submission_one()
    {
        var action = typeof(FeedSubmissionApiController).GetMethod(nameof(FeedSubmissionApiController.ImageProxy))!;

        action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName
            .Should().Be(FeedSubmissionRateLimiting.ImageProxyPolicyName);
    }
}

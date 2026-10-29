using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
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
}

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

internal static class TestImageProxy
{
    public static FeedSubmissionImageProxyService Create() => new(
        new FeedSubmissionImageProxyHttpClient(new HttpClient()),
        new MemoryCache(new MemoryCacheOptions()),
        new EphemeralDataProtectionProvider(),
        NullLogger<FeedSubmissionImageProxyService>.Instance);
}

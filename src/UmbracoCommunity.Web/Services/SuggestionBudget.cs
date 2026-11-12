using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using UmbracoCommunity.SearchTracker.Recording;
using UmbracoCommunity.Web.Abstract.Services;

namespace UmbracoCommunity.Web.Services;

internal sealed class SuggestionBudget : ISuggestionBudget, IDisposable
{
    internal const int PerVisitorPerHour = 20;
    internal const int SiteWidePerMinute = 60;

    private readonly MemoryCache _counters = new(new MemoryCacheOptions());
    private readonly TimeProvider _timeProvider;

    public SuggestionBudget(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public bool TryConsume(HttpContext? httpContext)
    {
        if (httpContext is null || SearchClient.IsBot(httpContext)) return false;

        var minute = _timeProvider.GetUtcNow().ToUnixTimeSeconds() / 60;
        return Consume($"site|{minute}", SiteWidePerMinute, TimeSpan.FromMinutes(2))
            && Consume($"visitor|{SearchClient.GetKey(httpContext)}", PerVisitorPerHour, TimeSpan.FromHours(1));
    }

    private bool Consume(string key, int limit, TimeSpan window)
    {
        var counter = _counters.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter();
        })!;
        return Interlocked.Increment(ref counter.Value) <= limit;
    }

    public void Dispose() => _counters.Dispose();

    private sealed class Counter
    {
        public int Value;
    }
}

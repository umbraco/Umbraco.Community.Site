using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.SearchTracker.Recording;

public interface ISearchTermRecorder
{
    /// <summary>Queues a search for logging. Never throws and never blocks on I/O.</summary>
    void Record(Guid siteKey, string? query, int resultCount, HttpContext httpContext);
}

/// <summary>
/// Filters out bots, repeat searches and floods before queueing. The client address is only used
/// as an in-memory key for those checks and is never stored.
/// </summary>
internal sealed class SearchTermRecorder : ISearchTermRecorder, IDisposable
{
    private readonly SearchEventChannel _channel;
    private readonly IOptions<SearchTrackerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public SearchTermRecorder(SearchEventChannel channel, IOptions<SearchTrackerOptions> options, TimeProvider timeProvider)
    {
        _channel = channel;
        _options = options;
        _timeProvider = timeProvider;
    }

    public void Record(Guid siteKey, string? query, int resultCount, HttpContext httpContext)
    {
        var options = _options.Value;
        if (!options.Enabled || siteKey == Guid.Empty) return;

        if (SearchClient.IsBot(httpContext) || SearchClient.IsFromDashboard(httpContext)) return;

        var term = SearchTermNormalizer.Normalize(query, options.MaxTermLength);
        if (term is null) return;

        var client = SearchClient.GetKey(httpContext);

        var duplicateKey = $"dup|{client}|{siteKey:N}|{term}";
        if (_cache.TryGetValue(duplicateKey, out _)) return;

        var counter = _cache.GetOrCreate($"rate|{client}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return new RateCounter();
        })!;
        if (Interlocked.Increment(ref counter.Value) > options.MaxSearchesPerClientPerHour) return;

        _cache.Set(duplicateKey, true, options.DuplicateWindow);

        _channel.Writer.TryWrite(new SearchEvent(siteKey, term, Math.Max(0, resultCount), _timeProvider.GetUtcNow().UtcDateTime));
    }

    public void Dispose() => _cache.Dispose();

    private sealed class RateCounter
    {
        public int Value;
    }
}

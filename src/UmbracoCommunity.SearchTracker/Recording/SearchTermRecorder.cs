using System.Net;
using System.Text.RegularExpressions;
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
internal sealed partial class SearchTermRecorder : ISearchTermRecorder, IDisposable
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

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent) || BotUserAgentRegex().IsMatch(userAgent)) return;

        var term = SearchTermNormalizer.Normalize(query, options.MaxTermLength);
        if (term is null) return;

        var client = GetClientKey(httpContext, userAgent);

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

    // Proxies and CDNs put the visitor's address in a header; the user agent keeps people behind one address apart.
    internal static string GetClientKey(HttpContext httpContext, string userAgent)
    {
        var headers = httpContext.Request.Headers;
        var address = FirstAddress(headers["CF-Connecting-IP"].ToString())
            ?? FirstAddress(headers["X-Forwarded-For"].ToString())
            ?? httpContext.Connection.RemoteIpAddress?.ToString();
        return $"{address}|{userAgent}";
    }

    private static string? FirstAddress(string header)
    {
        var first = header.Split(',')[0].Trim();
        if (IPAddress.TryParse(first, out var address)) return address.ToString();
        return IPEndPoint.TryParse(first, out var endPoint) ? endPoint.Address.ToString() : null;
    }

    public void Dispose() => _cache.Dispose();

    private sealed class RateCounter
    {
        public int Value;
    }

    [GeneratedRegex(@"bot|crawl|spider|slurp|scrape|preview|headless|lighthouse|monitor|python|curl|wget|httpclient|java/|go-http|okhttp|axios|node-fetch|postman",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BotUserAgentRegex();
}

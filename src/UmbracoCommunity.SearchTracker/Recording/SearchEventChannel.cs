using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.SearchTracker.Recording;

public sealed record SearchEvent(Guid SiteKey, string Term, int ResultCount, DateTime OccurredUtc);

/// <summary>Bounded queue between the search request and the database; drops events when full.</summary>
public sealed class SearchEventChannel
{
    private readonly Channel<SearchEvent> _channel;

    public SearchEventChannel(IOptions<SearchTrackerOptions> options)
    {
        _channel = Channel.CreateBounded<SearchEvent>(new BoundedChannelOptions(options.Value.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelWriter<SearchEvent> Writer => _channel.Writer;
    public ChannelReader<SearchEvent> Reader => _channel.Reader;
}

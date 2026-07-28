using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Umbraco.Community.NotFoundTracker.Configuration;

namespace Umbraco.Community.NotFoundTracker.Recording;

/// <summary>
/// Singleton bounded channel that decouples the request hot path from DB I/O.
/// Capacity is configured via <see cref="NotFoundTrackerOptions.ChannelCapacity"/>.
/// Full mode is <c>DropWrite</c>: when the channel is full, new events are silently dropped
/// — better to lose telemetry than to slow down the site under a flood.
/// </summary>
public sealed class NotFoundHitChannel
{
    private readonly Channel<NotFoundHitEvent> _channel;

    public NotFoundHitChannel(IOptions<NotFoundTrackerOptions> options)
    {
        _channel = Channel.CreateBounded<NotFoundHitEvent>(new BoundedChannelOptions(options.Value.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelWriter<NotFoundHitEvent> Writer => _channel.Writer;
    public ChannelReader<NotFoundHitEvent> Reader => _channel.Reader;
}

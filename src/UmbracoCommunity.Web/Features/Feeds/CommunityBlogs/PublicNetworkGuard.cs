using System.Net;
using System.Net.Sockets;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// SSRF guard shared between <see cref="FeedSubmissionImageProxyService"/>'s fail-fast pre-check and the
/// <c>SocketsHttpHandler.ConnectCallback</c> registered in <c>RegisterFeeds</c>. The callback is the actual
/// enforcement point: it resolves the host and connects directly to the validated IP, so there's no window
/// between "checked" and "connected" for a DNS-rebinding attack to exploit.
/// </summary>
public static class PublicNetworkGuard
{
    /// <summary>Resolves <paramref name="host"/> and returns the first address that isn't private/loopback/link-local, or null.</summary>
    public static async Task<IPAddress?> ResolvePublicAddressAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses.FirstOrDefault(IsPublicAddress);
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>
    /// <c>SocketsHttpHandler.ConnectCallback</c> that resolves the host, refuses non-public addresses and
    /// connects straight to the validated IP, so no DNS-rebinding window exists between check and connect.
    /// </summary>
    public static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var address = await ResolvePublicAddressAsync(context.DnsEndPoint.Host, cancellationToken)
            ?? throw new HttpRequestException($"'{context.DnsEndPoint.Host}' does not resolve to a permitted address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => false, // 10.0.0.0/8
                127 => false, // 127.0.0.0/8
                169 when bytes[1] == 254 => false, // 169.254.0.0/16 (link-local + cloud metadata)
                172 when bytes[1] is >= 16 and <= 31 => false, // 172.16.0.0/12
                192 when bytes[1] == 168 => false, // 192.168.0.0/16
                0 => false, // 0.0.0.0/8
                _ => true,
            };
        }

        // IPv6 unique local addresses (fc00::/7 — the IPv6 equivalent of the private IPv4 ranges above).
        var firstByte = address.GetAddressBytes()[0];
        return (firstByte & 0xFE) != 0xFC;
    }
}

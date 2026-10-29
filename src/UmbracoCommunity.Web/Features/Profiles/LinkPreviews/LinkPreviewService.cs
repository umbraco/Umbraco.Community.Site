using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

namespace UmbracoCommunity.Web.Features.Profiles.LinkPreviews;

/// <summary>
/// Fetches the title, share image and icon of a member-supplied link, always from the server and never from the
/// visitor's browser. Only https is fetched, every hop of a redirect chain is re-checked, connections are limited
/// to public addresses by <see cref="PublicNetworkGuard"/>, and the time spent and bytes read are capped. Any
/// failure yields <see cref="LinkPreview.Unreachable"/> rather than an exception.
/// </summary>
public sealed class LinkPreviewService
{
    private const int MaxRedirects = 3;
    private const int MaxHtmlBytes = 256 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    private readonly HttpClient _http;
    private readonly ILogger<LinkPreviewService> _logger;

    public LinkPreviewService(LinkPreviewHttpClient typedClient, ILogger<LinkPreviewService> logger)
    {
        _http = typedClient.Client;
        _logger = logger;
    }

    public async Task<LinkPreview> FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (!ProfileLinkUrl.TryParse(url, out var uri))
        {
            return LinkPreview.Unreachable;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                if (await PublicNetworkGuard.ResolvePublicAddressAsync(uri.Host, timeout.Token) is null)
                {
                    return LinkPreview.Unreachable;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.ParseAdd("text/html");
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (response.Headers.Location is null
                        || !ProfileLinkUrl.TryParse(new Uri(uri, response.Headers.Location).ToString(), out uri))
                    {
                        return LinkPreview.Unreachable;
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode
                    || response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml"))
                {
                    return LinkPreview.Unreachable;
                }

                var html = await ReadLimitedAsync(response, timeout.Token);
                return LinkPreviewHtmlParser.Parse(html, uri);
            }

            return LinkPreview.Unreachable;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or UriFormatException or RegexMatchTimeoutException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogDebug(ex, "Link preview fetch failed for {Host}.", uri.Host);
            return LinkPreview.Unreachable;
        }
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxHtmlBytes];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }
}

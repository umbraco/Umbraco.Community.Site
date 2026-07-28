using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public sealed class CommunityBlogsApiClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;

    public CommunityBlogsApiClient(CommunityBlogsHttpClient typedClient, IOptionsMonitor<CommunityBlogsOptions> options)
    {
        _http = typedClient.Client;
        _options = options;
    }

    /// <summary>Fetches one page of blog posts. <paramref name="cursor"/> null fetches the first page.</summary>
    public async Task<PostsResponseDto?> GetBlogPostsAsync(string? cursor, int limit, CancellationToken cancellationToken)
    {
        var requestUri = $"blog-posts?limit={limit.ToString(CultureInfo.InvariantCulture)}";
        if (!string.IsNullOrEmpty(cursor))
        {
            requestUri += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("Authorization", _options.CurrentValue.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PostsResponseDto>(CommunityBlogsJsonOptions.Default, cancellationToken);
    }

    /// <summary>Fetches-and-parses a feed URL without persisting, returning the posts it would produce.</summary>
    public async Task<PostsResponseDto?> PreviewFeedAsync(string feedUrl, string? name, string? github, int limit, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "blog-posts/preview")
            {
                Content = CreateJsonContent(new { url = feedUrl, name = name ?? string.Empty, github = github ?? string.Empty, limit }),
            },
            cancellationToken);

        return await response.Content.ReadFromJsonAsync<PostsResponseDto>(CommunityBlogsJsonOptions.Default, cancellationToken);
    }

    /// <summary>Looks up whether a feed URL is already listed, has a pending submission, or neither.</summary>
    public async Task<FeedSubmissionStatusResponseDto?> GetFeedSubmissionStatusAsync(string feedUrl, CancellationToken cancellationToken)
    {
        var requestUri = $"feed-submissions/status?url={Uri.EscapeDataString(feedUrl)}";
        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, requestUri),
            cancellationToken);

        return await response.Content.ReadFromJsonAsync<FeedSubmissionStatusResponseDto>(CommunityBlogsJsonOptions.Default, cancellationToken);
    }

    /// <summary>Submits a feed URL for manual review (idempotent).</summary>
    public async Task<FeedSubmissionResponseDto?> SubmitFeedAsync(string feedUrl, string? name, string? github, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Put, "feed-submissions")
            {
                Content = CreateJsonContent(new { url = feedUrl, name = name ?? string.Empty, github = github ?? string.Empty }),
            },
            cancellationToken);

        return await response.Content.ReadFromJsonAsync<FeedSubmissionResponseDto>(CommunityBlogsJsonOptions.Default, cancellationToken);
    }

    /// <summary>
    /// Builds a JSON request body via <see cref="StringContent"/> rather than <see cref="JsonContent"/>. The
    /// upstream API 500s on a chunked-encoded request body, and <see cref="JsonContent"/> never sets Content-Length (it has
    /// no <c>TryComputeLength</c> override), which forces HTTP/1.1 to fall back to Transfer-Encoding: chunked.
    /// </summary>
    private static StringContent CreateJsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value, CommunityBlogsJsonOptions.Default), Encoding.UTF8, "application/json");

    /// <summary>
    /// Sends a request, retrying once after a short delay on a 5xx response. The upstream API has been observed to
    /// intermittently return a transient 500 that succeeds on an immediate retry; 4xx responses are not retried
    /// since they indicate a genuine client-side problem (e.g. an invalid feed URL).
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            response?.Dispose();

            using var request = requestFactory();
            request.Headers.TryAddWithoutValidation("Authorization", _options.CurrentValue.ApiKey);

            response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || (int)response.StatusCode < 500 || attempt == maxAttempts)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken);
        }

        if (!response!.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();

            var detail = TryParseErrorDetail(errorBody);
            var message = detail?.Message ?? $"Request to the content platform failed with {(int)statusCode} {statusCode}. Body: {errorBody}";
            throw new CommunityBlogsApiException(statusCode, detail?.Code, message);
        }

        return response;
    }

    private static CommunityBlogsErrorDetail? TryParseErrorDetail(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<CommunityBlogsErrorEnvelope>(body, CommunityBlogsJsonOptions.Default)?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

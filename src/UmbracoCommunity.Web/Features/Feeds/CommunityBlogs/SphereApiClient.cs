using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public sealed class SphereApiClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;

    public SphereApiClient(SphereHttpClient typedClient, IOptionsMonitor<CommunityBlogsOptions> options)
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

        return await response.Content.ReadFromJsonAsync<PostsResponseDto>(SphereJsonOptions.Default, cancellationToken);
    }
}

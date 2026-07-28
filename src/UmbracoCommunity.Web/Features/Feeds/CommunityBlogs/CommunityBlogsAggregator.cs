using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Walks the cursor-paginated external blog-posts API, maps to <see cref="CommunityBlogPost"/>,
/// and returns the newest-first set. Returns null when nothing could be fetched (so callers
/// keep any previously-persisted data).
/// </summary>
public sealed class CommunityBlogsAggregator
{
    private readonly CommunityBlogsApiClient _client;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<CommunityBlogsAggregator> _logger;

    public CommunityBlogsAggregator(
        CommunityBlogsApiClient client,
        IOptionsMonitor<CommunityBlogsOptions> options,
        TimeProvider time,
        ILogger<CommunityBlogsAggregator> logger)
    {
        _client = client;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<CommunityBlogsData?> BuildAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            _logger.LogWarning("CommunityBlogsOptions.ApiKey or ApiBaseUrl is not configured; skipping refresh.");
            return null;
        }

        var posts = new List<CommunityBlogPost>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        var anySuccess = false;

        while (posts.Count < options.MaxPosts)
        {
            PostsResponseDto? page;
            try
            {
                page = await _client.GetBlogPostsAsync(cursor, options.FetchBatchSize, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch a page of community blog posts (cursor={Cursor}).", cursor);
                break;
            }

            if (page is null)
            {
                break;
            }

            // A successful fetch — even one returning zero posts — counts: an empty result
            // is a legitimate "no posts" state. Only a total failure to reach the API (no
            // page ever returned) leaves anySuccess false and yields null below.
            anySuccess = true;

            foreach (var dto in page.Data)
            {
                var mapped = Map(dto);
                if (mapped is not null)
                {
                    posts.Add(mapped);
                }

                if (posts.Count >= options.MaxPosts)
                {
                    break;
                }
            }

            if (!page.Pagination.HasMore || string.IsNullOrEmpty(page.Pagination.NextCursor))
            {
                break;
            }

            if (!seenCursors.Add(page.Pagination.NextCursor))
            {
                _logger.LogWarning("Repeated cursor {Cursor} from the external API; stopping walk.", page.Pagination.NextCursor);
                break;
            }

            cursor = page.Pagination.NextCursor;
        }

        if (!anySuccess)
        {
            return null;
        }

        // posts.Count is already bounded by the while-condition, so Take(MaxPosts) is a
        // defensive safety belt rather than a functional trim.
        var ordered = posts
            .OrderByDescending(p => p.PublishedAt)
            .Take(options.MaxPosts)
            .ToArray();

        return new CommunityBlogsData(ordered, _time.GetUtcNow());
    }

    internal static CommunityBlogPost? Map(PublicPostDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Url) || string.IsNullOrWhiteSpace(dto.Title))
        {
            return null;
        }

        return new CommunityBlogPost(
            Id: dto.Id,
            Title: DecodeText(dto.Title)!,
            Url: dto.Url!.Trim(),
            Excerpt: DecodeText(dto.Content),
            CoverImageUrl: string.IsNullOrWhiteSpace(dto.CoverImageUrl) ? null : dto.CoverImageUrl!.Trim(),
            PublishedAt: dto.PublishedAt,
            AuthorName: DecodeText(dto.Author?.Name),
            AuthorAvatarUrl: string.IsNullOrWhiteSpace(dto.Author?.AvatarUrl) ? null : dto.Author!.AvatarUrl!.Trim(),
            AuthorProfileUrl: string.IsNullOrWhiteSpace(dto.Author?.ProfileUrl) ? null : dto.Author!.ProfileUrl!.Trim());
    }

    /// <summary>
    /// Trims display text and HTML-decodes it. The upstream feed sometimes returns
    /// entity-encoded text (e.g. "Here&amp;#x27;s"); decoding here means the Razor view's
    /// automatic encoding renders it correctly instead of showing the raw entity.
    /// </summary>
    private static string? DecodeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlDecode(value).Trim();
}

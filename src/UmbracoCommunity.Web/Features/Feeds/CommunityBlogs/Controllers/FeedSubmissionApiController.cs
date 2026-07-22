using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs.Models;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs.Controllers;

[ApiController]
[Route("api/feed-submission")]
[EnableRateLimiting(FeedSubmissionRateLimiting.PolicyName)]
public class FeedSubmissionApiController : ControllerBase
{
    private const int PreviewPostLimit = 6;

    private readonly CommunityBlogsApiClient _blogsApiClient;
    private readonly FeedSubmissionImageProxyService _imageProxy;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly ILogger<FeedSubmissionApiController> _logger;

    public FeedSubmissionApiController(
        CommunityBlogsApiClient blogsApiClient,
        FeedSubmissionImageProxyService imageProxy,
        IOptionsMonitor<CommunityBlogsOptions> options,
        ILogger<FeedSubmissionApiController> logger)
    {
        _blogsApiClient = blogsApiClient;
        _imageProxy = imageProxy;
        _options = options;
        _logger = logger;
    }

    /// <summary>Previews how a feed URL would render as Community Blog cards, without persisting anything.</summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(FeedPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromBody] FeedSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Honeypot))
        {
            return Ok(new FeedPreviewResponse(Array.Empty<PublicPostDto>(), "none"));
        }

        if (!IsValidFeedUrl(request.FeedUrl))
        {
            return BadRequest(new { error = "Please provide a valid absolute http or https feed URL." });
        }

        if (!IsConfigured())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Feed preview isn't available right now. Please try again later." });
        }

        try
        {
            var previewTask = _blogsApiClient.PreviewFeedAsync(request.FeedUrl!, request.Name, request.GithubUsername, PreviewPostLimit, cancellationToken);
            var statusTask = GetFeedStatusAsync(request.FeedUrl!, cancellationToken);
            await Task.WhenAll(previewTask, statusTask);

            var posts = (previewTask.Result?.Data ?? [])
                .Select(post => post with
                {
                    CoverImageUrl = _imageProxy.CreateProxyUrl(ResolveToAbsolute(post.CoverImageUrl)),
                    Author = post.Author is null
                        ? null
                        : post.Author with { AvatarUrl = _imageProxy.CreateProxyUrl(ResolveToAbsolute(post.Author.AvatarUrl)) },
                })
                .ToList();

            return Ok(new FeedPreviewResponse(posts, statusTask.Result));
        }
        catch (CommunityBlogsApiException ex) when (!ex.IsServerError)
        {
            // The platform rejected the feed itself (e.g. invalid_feed) — relay its message, it's actionable by the user.
            return BadRequest(new { error = ex.Message });
        }
        catch (CommunityBlogsApiException ex)
        {
            _logger.LogWarning(ex, "The content platform returned a server error previewing feed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the preview service. Please try again later." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error previewing feed via the content platform");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the preview service. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse feed preview response from the content platform");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from the preview service." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error previewing feed via the content platform");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>Submits a feed URL for manual review.</summary>
    [HttpPut("submit")]
    [ProducesResponseType(typeof(FeedSubmissionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] FeedSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Honeypot))
        {
            return Ok(new FeedSubmissionResponseDto(null, request.FeedUrl ?? string.Empty, request.Name, request.GithubUsername, "pending", null));
        }

        if (!IsValidFeedUrl(request.FeedUrl))
        {
            return BadRequest(new { error = "Please provide a valid absolute http or https feed URL." });
        }

        if (!IsConfigured())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Feed submission isn't available right now. Please try again later." });
        }

        try
        {
            var result = await _blogsApiClient.SubmitFeedAsync(request.FeedUrl!, request.Name, request.GithubUsername, cancellationToken);
            return Ok(result);
        }
        catch (CommunityBlogsApiException ex) when (!ex.IsServerError)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (CommunityBlogsApiException ex)
        {
            _logger.LogWarning(ex, "The content platform returned a server error submitting feed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the submission service. Please try again later." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error submitting feed via the content platform");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the submission service. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse feed submission response from the content platform");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from the submission service." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting feed via the content platform");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Streams a cover/avatar image from whatever domain a submitted feed happens to be on, so the preview
    /// can render it from 'self' without relaxing CSP img-src. See <see cref="FeedSubmissionImageProxyService"/>
    /// for the SSRF guard on the caller-supplied URL.
    /// </summary>
    [HttpGet("image-proxy")]
    public async Task<IActionResult> ImageProxy([FromQuery] string token, CancellationToken cancellationToken)
    {
        var result = await _imageProxy.FetchAsync(token, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=600";
        return File(result.Value.Bytes, result.Value.ContentType);
    }

    /// <summary>
    /// Looks up the feed's current submission status, defaulting to "none" on failure — this is a best-effort
    /// enhancement to the preview and shouldn't fail the whole preview if it errors.
    /// </summary>
    private async Task<string> GetFeedStatusAsync(string feedUrl, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _blogsApiClient.GetFeedSubmissionStatusAsync(feedUrl, cancellationToken);
            return result?.Status ?? "none";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch feed submission status; defaulting to none");
            return "none";
        }
    }

    private static bool IsValidFeedUrl(string? feedUrl) =>
        Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    /// <summary>
    /// ApiBaseUrl/ApiKey have no defaults (see CommunityBlogsOptions) — without this check, a
    /// missing ApiBaseUrl surfaces as an unhandled InvalidOperationException from HttpClient
    /// (no BaseAddress set) that falls into the generic catch below as an opaque 500.
    /// </summary>
    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_options.CurrentValue.ApiBaseUrl) && !string.IsNullOrWhiteSpace(_options.CurrentValue.ApiKey);

    private string? ResolveToAbsolute(string? url)
    {
        if (string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        return new Uri(new Uri(_options.CurrentValue.ApiBaseUrl), url).ToString();
    }
}

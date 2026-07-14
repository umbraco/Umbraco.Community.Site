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

    private readonly SphereApiClient _sphereClient;
    private readonly FeedSubmissionImageProxyService _imageProxy;
    private readonly IOptionsMonitor<CommunityBlogsOptions> _options;
    private readonly ILogger<FeedSubmissionApiController> _logger;

    public FeedSubmissionApiController(
        SphereApiClient sphereClient,
        FeedSubmissionImageProxyService imageProxy,
        IOptionsMonitor<CommunityBlogsOptions> options,
        ILogger<FeedSubmissionApiController> logger)
    {
        _sphereClient = sphereClient;
        _imageProxy = imageProxy;
        _options = options;
        _logger = logger;
    }

    /// <summary>Previews how a feed URL would render as Community Blog cards, without persisting anything.</summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromBody] FeedSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Honeypot))
        {
            return Ok(Array.Empty<PublicPostDto>());
        }

        if (!IsValidFeedUrl(request.FeedUrl))
        {
            return BadRequest(new { error = "Please provide a valid absolute http or https feed URL." });
        }

        try
        {
            var result = await _sphereClient.PreviewFeedAsync(request.FeedUrl!, request.Name, request.GithubUsername, PreviewPostLimit, cancellationToken);
            var posts = (result?.Data ?? [])
                .Select(post => post with
                {
                    CoverImageUrl = ResolveToAbsolute(post.CoverImageUrl),
                    Author = post.Author is null
                        ? null
                        : post.Author with { AvatarUrl = ResolveToAbsolute(post.Author.AvatarUrl) },
                })
                .ToList();

            // The image proxy only serves URLs it's seen returned from a preview call for this client —
            // register them here so the cards this response describes can actually load their images.
            _imageProxy.RegisterAllowedUrls(
                ClientKey,
                posts.SelectMany(post => new[] { post.CoverImageUrl, post.Author?.AvatarUrl }));

            return Ok(posts);
        }
        catch (SphereApiException ex) when (!ex.IsServerError)
        {
            // Sphere rejected the feed itself (e.g. invalid_feed) — relay its message, it's actionable by the user.
            return BadRequest(new { error = ex.Message });
        }
        catch (SphereApiException ex)
        {
            _logger.LogWarning(ex, "Sphere returned a server error previewing feed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the preview service. Please try again later." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error previewing feed via Sphere");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the preview service. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sphere feed preview response");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from the preview service." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error previewing feed via Sphere");
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

        try
        {
            var result = await _sphereClient.SubmitFeedAsync(request.FeedUrl!, request.Name, request.GithubUsername, cancellationToken);
            return Ok(result);
        }
        catch (SphereApiException ex) when (!ex.IsServerError)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (SphereApiException ex)
        {
            _logger.LogWarning(ex, "Sphere returned a server error submitting feed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the submission service. Please try again later." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error submitting feed via Sphere");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to reach the submission service. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sphere feed submission response");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from the submission service." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting feed via Sphere");
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
    public async Task<IActionResult> ImageProxy([FromQuery] string url, CancellationToken cancellationToken)
    {
        var result = await _imageProxy.FetchAsync(ClientKey, url, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=600";
        return File(result.Value.Bytes, result.Value.ContentType);
    }

    /// <summary>Same per-IP identity the rate limiter partitions on — not spoof-proof, but consistent with the
    /// existing precedent in this controller, and the image proxy's allowlist doesn't need to be stronger than that.</summary>
    private string ClientKey => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static bool IsValidFeedUrl(string? feedUrl) =>
        Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private string? ResolveToAbsolute(string? url)
    {
        if (string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        return new Uri(new Uri(_options.CurrentValue.ApiBaseUrl), url).ToString();
    }
}

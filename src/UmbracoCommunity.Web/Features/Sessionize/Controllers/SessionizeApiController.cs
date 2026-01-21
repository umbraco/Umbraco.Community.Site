using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;
using UmbracoCommunity.Web.Features.Sessionize.Models;

namespace UmbracoCommunity.Web.Features.Sessionize.Controllers;

[ApiController]
[Route("api/sessionize")]
public class SessionizeApiController : ControllerBase
{
    private readonly SessionizeApiClient _sessionizeClient;
    private readonly ILogger<SessionizeApiController> _logger;

    public SessionizeApiController(SessionizeApiClient sessionizeClient, ILogger<SessionizeApiController> logger)
    {
        _sessionizeClient = sessionizeClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets all sessions
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<SessionizeSession>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _sessionizeClient.GetSessionsAsync(cancellationToken);
            return Ok(sessions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching sessions from Sessionize");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to connect to Sessionize. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sessionize API response for sessions");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from Sessionize." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Sessionize configuration error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching sessions from Sessionize");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Gets a specific session by ID
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(SessionizeSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "sessionId" })]
    public async Task<IActionResult> GetSession(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessionizeClient.GetSessionByIdAsync(sessionId, cancellationToken);
            if (session == null)
            {
                return NotFound(new { error = $"Session with ID '{sessionId}' not found" });
            }
            return Ok(session);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching session {SessionId} from Sessionize", sessionId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to connect to Sessionize. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sessionize API response for session {SessionId}", sessionId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from Sessionize." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Sessionize configuration error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching session {SessionId} from Sessionize", sessionId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Gets all speakers
    /// </summary>
    [HttpGet("speakers")]
    [ProducesResponseType(typeof(List<SessionizeSpeaker>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetSpeakers(CancellationToken cancellationToken)
    {
        try
        {
            var speakers = await _sessionizeClient.GetSpeakersAsync(cancellationToken);
            return Ok(speakers);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching speakers from Sessionize");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to connect to Sessionize. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sessionize API response for speakers");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from Sessionize." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Sessionize configuration error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching speakers from Sessionize");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Gets a specific speaker by ID
    /// </summary>
    [HttpGet("speakers/{speakerId}")]
    [ProducesResponseType(typeof(SessionizeSpeaker), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "speakerId" })]
    public async Task<IActionResult> GetSpeaker(string speakerId, CancellationToken cancellationToken)
    {
        try
        {
            var speaker = await _sessionizeClient.GetSpeakerByIdAsync(speakerId, cancellationToken);
            if (speaker == null)
            {
                return NotFound(new { error = $"Speaker with ID '{speakerId}' not found" });
            }
            return Ok(speaker);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching speaker {SpeakerId} from Sessionize", speakerId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to connect to Sessionize. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sessionize API response for speaker {SpeakerId}", speakerId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from Sessionize." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Sessionize configuration error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching speaker {SpeakerId} from Sessionize", speakerId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Gets the schedule in grid format
    /// </summary>
    [HttpGet("schedule")]
    [ProducesResponseType(typeof(List<SessionizeSchedule>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetSchedule(CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await _sessionizeClient.GetScheduleAsync(cancellationToken);
            return Ok(schedule);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching schedule from Sessionize");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to connect to Sessionize. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sessionize API response for schedule");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from Sessionize." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Sessionize configuration error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching schedule from Sessionize");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Gets sessions in a format suitable for Contentment data sources.
    /// Returns only talk sessions (excludes service sessions like breaks/lunch).
    /// </summary>
    [HttpGet("sessions.json")]
    [ProducesResponseType(typeof(List<ContentmentDataItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetSessionsForContentment(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _sessionizeClient.GetSessionsAsync(cancellationToken);

            // Filter to only talk sessions and format for Contentment
            var contentmentData = sessions
                .Where(s => !s.IsServiceSession)
                .OrderBy(s => s.StartsAt)
                .ThenBy(s => s.Title)
                .Select(s => new ContentmentDataItem
                {
                    Name = FormatSessionName(s),
                    Value = s.Id
                })
                .ToList();

            return Ok(contentmentData);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching sessions from Sessionize for Contentment");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to connect to Sessionize. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sessionize API response for Contentment sessions");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from Sessionize." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Sessionize configuration error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching sessions from Sessionize for Contentment");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }

    private static string FormatSessionName(SessionizeSession session)
    {
        var speakers = session.Speakers?.Count > 0
            ? $" — {string.Join(", ", session.Speakers.Select(s => s.FullName))}"
            : string.Empty;

        return $"{session.Title}{speakers}";
    }

    /// <summary>
    /// Gets all categories
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<SessionizeCategory>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _sessionizeClient.GetCategoriesAsync(cancellationToken);
            return Ok(categories);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching categories from Sessionize");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to connect to Sessionize. Please try again later." });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Sessionize API response for categories");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Received invalid data from Sessionize." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Sessionize configuration error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching categories from Sessionize");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred. Please try again." });
        }
    }
}

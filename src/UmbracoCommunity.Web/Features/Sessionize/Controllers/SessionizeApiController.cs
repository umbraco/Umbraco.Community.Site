using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmbracoCommunity.Web.Features.Sessionize.Infrastructure;
using UmbracoCommunity.Web.Features.Sessionize.Models;

namespace UmbracoCommunity.Web.Features.Sessionize.Controllers;

[ApiController]
[Route("api/sessionize")]
public class SessionizeApiController : ControllerBase
{
    private readonly SessionizeApiClient _sessionizeClient;

    public SessionizeApiController(SessionizeApiClient sessionizeClient)
    {
        _sessionizeClient = sessionizeClient;
    }

    /// <summary>
    /// Gets all sessions grouped by category
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<SessionizeSessionGroup>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(Duration = 300, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _sessionizeClient.GetSessionsAsync(cancellationToken);
            return Ok(sessions);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to fetch sessions from Sessionize" });
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
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to fetch session from Sessionize" });
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
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to fetch speakers from Sessionize" });
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
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to fetch speaker from Sessionize" });
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
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Unable to fetch schedule from Sessionize" });
        }
    }
}

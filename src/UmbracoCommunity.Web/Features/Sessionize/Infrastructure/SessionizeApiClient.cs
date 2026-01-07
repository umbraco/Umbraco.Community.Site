using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoCommunity.Web.Features.Sessionize.Models;

namespace UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

public class SessionizeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SessionizeOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SessionizeApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SessionizeApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<SessionizeOptions> options,
        IMemoryCache cache,
        ILogger<SessionizeApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets all sessions grouped by category
    /// </summary>
    public async Task<List<SessionizeSessionGroup>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"sessionize_sessions_{_options.EventId}";

        if (_cache.TryGetValue(cacheKey, out List<SessionizeSessionGroup>? cachedSessions) && cachedSessions != null)
        {
            _logger.LogDebug("Returning cached sessions for event {EventId}", _options.EventId);
            return cachedSessions;
        }

        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Sessionize is not configured. Please set the EventId in configuration.");
            return new List<SessionizeSessionGroup>();
        }

        try
        {
            var url = _options.GetApiUrl("Sessions");
            _logger.LogInformation("Fetching sessions from Sessionize: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var sessions = JsonSerializer.Deserialize<List<SessionizeSessionGroup>>(json, JsonOptions)
                ?? new List<SessionizeSessionGroup>();

            _cache.Set(cacheKey, sessions, TimeSpan.FromMinutes(_options.CacheDurationMinutes));

            _logger.LogInformation("Fetched {GroupCount} session groups from Sessionize", sessions.Count);
            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching sessions from Sessionize for event {EventId}", _options.EventId);
            throw;
        }
    }

    /// <summary>
    /// Gets all speakers
    /// </summary>
    public async Task<List<SessionizeSpeaker>> GetSpeakersAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"sessionize_speakers_{_options.EventId}";

        if (_cache.TryGetValue(cacheKey, out List<SessionizeSpeaker>? cachedSpeakers) && cachedSpeakers != null)
        {
            _logger.LogDebug("Returning cached speakers for event {EventId}", _options.EventId);
            return cachedSpeakers;
        }

        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Sessionize is not configured. Please set the EventId in configuration.");
            return new List<SessionizeSpeaker>();
        }

        try
        {
            var url = _options.GetApiUrl("Speakers");
            _logger.LogInformation("Fetching speakers from Sessionize: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var speakers = JsonSerializer.Deserialize<List<SessionizeSpeaker>>(json, JsonOptions)
                ?? new List<SessionizeSpeaker>();

            _cache.Set(cacheKey, speakers, TimeSpan.FromMinutes(_options.CacheDurationMinutes));

            _logger.LogInformation("Fetched {SpeakerCount} speakers from Sessionize", speakers.Count);
            return speakers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching speakers from Sessionize for event {EventId}", _options.EventId);
            throw;
        }
    }

    /// <summary>
    /// Gets the schedule in a grid format (GridSmart view)
    /// </summary>
    public async Task<List<SessionizeSchedule>> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"sessionize_schedule_{_options.EventId}";

        if (_cache.TryGetValue(cacheKey, out List<SessionizeSchedule>? cachedSchedule) && cachedSchedule != null)
        {
            _logger.LogDebug("Returning cached schedule for event {EventId}", _options.EventId);
            return cachedSchedule;
        }

        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Sessionize is not configured. Please set the EventId in configuration.");
            return new List<SessionizeSchedule>();
        }

        try
        {
            var url = _options.GetApiUrl("GridSmart");
            _logger.LogInformation("Fetching schedule from Sessionize: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var schedule = JsonSerializer.Deserialize<List<SessionizeSchedule>>(json, JsonOptions)
                ?? new List<SessionizeSchedule>();

            _cache.Set(cacheKey, schedule, TimeSpan.FromMinutes(_options.CacheDurationMinutes));

            _logger.LogInformation("Fetched schedule with {DayCount} days from Sessionize", schedule.Count);
            return schedule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schedule from Sessionize for event {EventId}", _options.EventId);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific speaker by ID
    /// </summary>
    public async Task<SessionizeSpeaker?> GetSpeakerByIdAsync(string speakerId, CancellationToken cancellationToken = default)
    {
        var speakers = await GetSpeakersAsync(cancellationToken);
        return speakers.FirstOrDefault(s => s.Id == speakerId);
    }

    /// <summary>
    /// Gets a specific session by ID
    /// </summary>
    public async Task<SessionizeSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var sessionGroups = await GetSessionsAsync(cancellationToken);
        return sessionGroups
            .SelectMany(g => g.Sessions)
            .FirstOrDefault(s => s.Id == sessionId);
    }

    /// <summary>
    /// Clears the cache for all Sessionize data
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove($"sessionize_sessions_{_options.EventId}");
        _cache.Remove($"sessionize_speakers_{_options.EventId}");
        _cache.Remove($"sessionize_schedule_{_options.EventId}");
        _logger.LogInformation("Cleared Sessionize cache for event {EventId}", _options.EventId);
    }
}

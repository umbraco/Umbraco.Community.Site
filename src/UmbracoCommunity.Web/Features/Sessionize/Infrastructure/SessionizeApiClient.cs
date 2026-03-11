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
        _httpClient = httpClientFactory.CreateClient("Sessionize");
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets all data from Sessionize (sessions, speakers, categories, rooms)
    /// This is the primary data source - cached and used by other methods
    /// </summary>
    public async Task<SessionizeAllData> GetAllDataAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"sessionize_all_{_options.EventId}";

        if (_cache.TryGetValue(cacheKey, out SessionizeAllData? cachedData) && cachedData != null)
        {
            _logger.LogDebug("Returning cached Sessionize data for event {EventId}", _options.EventId);
            return cachedData;
        }

        if (!_options.IsConfigured)
        {
            _logger.LogError("Sessionize is not configured. Please set the EventId in appsettings.");
            throw new InvalidOperationException("Sessionize is not configured. Please set the EventId in appsettings.");
        }

        try
        {
            var url = _options.GetApiUrl("All");
            _logger.LogInformation("Fetching all data from Sessionize: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var allData = JsonSerializer.Deserialize<SessionizeAllData>(json, JsonOptions);
            if (allData == null)
            {
                _logger.LogError("Failed to deserialize Sessionize API response. Response was empty or invalid.");
                throw new InvalidOperationException("Sessionize API returned an unexpected response format.");
            }

            allData.PronounsQuestionId = allData.Questions
                .FirstOrDefault(q => q.Question.Contains("Pronouns", StringComparison.OrdinalIgnoreCase))
                ?.Id;

            _cache.Set(cacheKey, allData, TimeSpan.FromMinutes(_options.CacheDurationInMinutes));

            _logger.LogInformation(
                "Fetched Sessionize data: {SessionCount} sessions, {SpeakerCount} speakers, {CategoryCount} categories",
                allData.Sessions.Count, allData.Speakers.Count, allData.Categories.Count);

            return allData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from Sessionize for event {EventId}", _options.EventId);
            throw;
        }
    }

    /// <summary>
    /// Gets all sessions with full speaker data populated
    /// </summary>
    public async Task<List<SessionizeSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var allData = await GetAllDataAsync(cancellationToken);
        var speakerLookup = allData.Speakers.ToDictionary(s => s.Id);
        var sessionLookup = allData.Sessions.ToDictionary(s => s.Id);

        return allData.Sessions
            .Select(raw => MapToSession(raw, speakerLookup, sessionLookup, allData.PronounsQuestionId))
            .ToList();
    }

    /// <summary>
    /// Gets all speakers with session data populated
    /// </summary>
    public async Task<List<SessionizeSpeaker>> GetSpeakersAsync(CancellationToken cancellationToken = default)
    {
        var allData = await GetAllDataAsync(cancellationToken);
        var sessionLookup = allData.Sessions.ToDictionary(s => s.Id);

        return allData.Speakers.Select(raw => MapToSpeaker(raw, sessionLookup, allData.PronounsQuestionId)).ToList();
    }

    /// <summary>
    /// Gets all categories
    /// </summary>
    public async Task<List<SessionizeCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var allData = await GetAllDataAsync(cancellationToken);
        return allData.Categories;
    }

    /// <summary>
    /// Gets all rooms
    /// </summary>
    public async Task<List<SessionizeRoomInfo>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        var allData = await GetAllDataAsync(cancellationToken);
        return allData.Rooms;
    }

    /// <summary>
    /// Gets the schedule in a grid format, built from the "All" data
    /// </summary>
    public async Task<List<SessionizeSchedule>> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        var allData = await GetAllDataAsync(cancellationToken);
        var speakerLookup = allData.Speakers.ToDictionary(s => s.Id);
        var rawSessionLookup = allData.Sessions.ToDictionary(s => s.Id);

        // Sort rooms once and cache the list
        var sortedRooms = allData.Rooms.OrderBy(r => r.Sort).ToList();

        // Pre-map all sessions to avoid repeated mapping
        var sessionLookup = allData.Sessions.ToDictionary(
            s => s.Id,
            s => MapToSession(s, speakerLookup, rawSessionLookup, allData.PronounsQuestionId));

        // Group sessions by date, then by time slot
        var sessionsByDate = allData.Sessions
            .Where(s => s.StartsAt.HasValue)
            .GroupBy(s => s.StartsAt!.Value.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var schedule = new List<SessionizeSchedule>();

        // Pre-build the rooms list for the schedule header (same for all days)
        var scheduleRooms = sortedRooms
            .Select(r => new SessionizeRoom { Id = r.Id, Name = r.Name })
            .ToList();

        foreach (var dateGroup in sessionsByDate)
        {
            // Build lookup of sessions by room for this time group to avoid repeated FirstOrDefault calls
            var timeSlots = dateGroup
                .GroupBy(s => s.StartsAt!.Value)
                .OrderBy(g => g.Key)
                .Select(timeGroup =>
                {
                    var sessionsByRoom = timeGroup.ToDictionary(s => s.RoomId ?? 0);
                    return new SessionizeTimeSlot
                    {
                        SlotStart = timeGroup.Key.ToString("HH:mm:ss"),
                        Rooms = sortedRooms
                            .Select(room => new SessionizeRoom
                            {
                                Id = room.Id,
                                Name = room.Name,
                                Session = sessionsByRoom.TryGetValue(room.Id, out var rawSession)
                                    ? sessionLookup[rawSession.Id]
                                    : null
                            })
                            .ToList()
                    };
                })
                .ToList();

            schedule.Add(new SessionizeSchedule
            {
                Date = dateGroup.Key.ToString("yyyy-MM-ddTHH:mm:ss"),
                IsDefault = dateGroup.Key.Date == DateTime.Today,
                Rooms = scheduleRooms,
                TimeSlots = timeSlots
            });
        }

        _logger.LogInformation("Built schedule with {DayCount} days from All data", schedule.Count);
        return schedule;
    }

    private static SessionizeSession MapToSession(SessionizeSessionRaw raw, Dictionary<string, SessionizeSpeakerRaw> speakerLookup, Dictionary<string, SessionizeSessionRaw> sessionLookup, int? pronounsQuestionId)
    {
        return new SessionizeSession
        {
            Id = raw.Id,
            Title = raw.Title,
            Description = raw.Description,
            StartsAt = raw.StartsAt,
            EndsAt = raw.EndsAt,
            IsServiceSession = raw.IsServiceSession,
            IsPlenumSession = raw.IsPlenumSession,
            RoomId = raw.RoomId,
            Room = raw.Room,
            LiveUrl = raw.LiveUrl,
            RecordingUrl = raw.RecordingUrl,
            Status = raw.Status,
            CategoryItems = raw.CategoryItems,
            Speakers = raw.SpeakerIds
                .Where(id => speakerLookup.ContainsKey(id))
                .Select(id => MapToSpeaker(speakerLookup[id], sessionLookup, pronounsQuestionId))
                .ToList()
        };
    }

    /// <summary>
    /// Gets a specific speaker by ID
    /// </summary>
    public async Task<SessionizeSpeaker?> GetSpeakerByIdAsync(string speakerId, CancellationToken cancellationToken = default)
    {
        var allData = await GetAllDataAsync(cancellationToken);
        var rawSpeaker = allData.Speakers.FirstOrDefault(s => s.Id == speakerId);
        if (rawSpeaker == null) return null;

        var sessionLookup = allData.Sessions.ToDictionary(s => s.Id);
        return MapToSpeaker(rawSpeaker, sessionLookup, allData.PronounsQuestionId);
    }

    /// <summary>
    /// Gets a specific session by ID
    /// </summary>
    public async Task<SessionizeSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var allData = await GetAllDataAsync(cancellationToken);
        var rawSession = allData.Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (rawSession == null) return null;

        var speakerLookup = allData.Speakers.ToDictionary(s => s.Id);
        var sessionLookup = allData.Sessions.ToDictionary(s => s.Id);
        return MapToSession(rawSession, speakerLookup, sessionLookup, allData.PronounsQuestionId);
    }

    /// <summary>
    /// Looks up a category item by ID
    /// </summary>
    public async Task<SessionizeCategoryItem?> GetCategoryItemByIdAsync(int categoryItemId, CancellationToken cancellationToken = default)
    {
        var categories = await GetCategoriesAsync(cancellationToken);
        return categories
            .SelectMany(c => c.Items)
            .FirstOrDefault(i => i.Id == categoryItemId);
    }

    /// <summary>
    /// Clears the cache for all Sessionize data
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove($"sessionize_all_{_options.EventId}");
        _logger.LogInformation("Cleared Sessionize cache for event {EventId}", _options.EventId);
    }

    private static SessionizeSpeaker MapToSpeaker(SessionizeSpeakerRaw raw, Dictionary<string, SessionizeSessionRaw> sessionLookup, int? pronounsQuestionId)
    {
        var pronouns = pronounsQuestionId.HasValue
            ? raw.QuestionAnswers.FirstOrDefault(qa => qa.QuestionId == pronounsQuestionId.Value)?.AnswerValue
            : null;

        return new SessionizeSpeaker
        {
            Id = raw.Id,
            FirstName = raw.FirstName,
            LastName = raw.LastName,
            FullName = raw.FullName,
            Bio = raw.Bio,
            TagLine = raw.TagLine,
            Pronouns = string.IsNullOrWhiteSpace(pronouns) ? null : pronouns.Trim(),
            ProfilePicture = raw.ProfilePicture,
            IsTopSpeaker = raw.IsTopSpeaker,
            Links = raw.Links,
            CategoryItems = raw.CategoryItems,
            Sessions = raw.SessionIds
                .Select(id => sessionLookup.TryGetValue(id.ToString(), out var session) ? session : null)
                .Where(s => s != null)
                .Select(s => new SessionizeSessionOverview { Id = int.TryParse(s!.Id, out var i) ? i : null, Name = s.Title })
                .ToList()
        };
    }
}

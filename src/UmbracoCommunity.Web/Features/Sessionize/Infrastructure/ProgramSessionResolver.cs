using UmbracoCommunity.Web.Features.Sessionize.Models;

namespace UmbracoCommunity.Web.Features.Sessionize.Infrastructure;

public class ProgramSessionResolver
{
    private static readonly TimeZoneInfo EventTimeZone = ResolveCopenhagenTimeZone();

    private readonly SessionizeApiClient _client;

    public ProgramSessionResolver(SessionizeApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// For each requested room, returns the session that is currently live (or the most recently started today),
    /// plus the next session after it. If nothing is live, "Current" is null and "Next" is the next upcoming session today.
    /// Pass <paramref name="nowOverride"/> in event-local time to simulate a different moment for testing.
    /// </summary>
    public async Task<List<RoomSessionStatus>> ResolveCurrentAsync(IEnumerable<int> roomIds, DateTime? nowOverride = null, CancellationToken cancellationToken = default)
    {
        var sessions = await _client.GetSessionsAsync(cancellationToken);
        var rooms = await _client.GetRoomsAsync(cancellationToken);
        var now = nowOverride ?? EventNow();

        var roomNames = rooms.ToDictionary(r => r.Id, r => r.Name);

        var byRoom = sessions
            .Where(s => s.RoomId.HasValue && s.StartsAt.HasValue && s.EndsAt.HasValue)
            .GroupBy(s => s.RoomId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartsAt).ToList());

        var result = new List<RoomSessionStatus>();
        foreach (var roomId in roomIds.Distinct())
        {
            var roomSessions = byRoom.TryGetValue(roomId, out var rs) ? rs : new List<SessionizeSession>();

            var current = roomSessions.FirstOrDefault(s => s.StartsAt!.Value <= now && now < s.EndsAt!.Value);
            var next = current != null
                ? roomSessions.FirstOrDefault(s => s.StartsAt > current.EndsAt)
                : roomSessions.FirstOrDefault(s => s.StartsAt > now);

            result.Add(new RoomSessionStatus
            {
                RoomId = roomId,
                RoomName = roomNames.TryGetValue(roomId, out var name) ? name : $"Room {roomId}",
                Current = current,
                Next = next,
            });
        }

        return result;
    }

    /// <summary>
    /// Returns the explicit list of highlighted sessions in the order the editor picked them.
    /// </summary>
    public async Task<List<SessionizeSession>> ResolveHighlightedAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken = default)
    {
        var sessions = await _client.GetSessionsAsync(cancellationToken);
        var lookup = sessions.ToDictionary(s => s.Id);

        return sessionIds
            .Select(id => lookup.TryGetValue(id, out var s) ? s : null)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();
    }

    /// <summary>
    /// Returns sessions filtered by day / room / category. Any filter that's null or empty matches everything.
    /// </summary>
    public async Task<List<SessionizeSession>> ResolveFilteredAsync(
        IEnumerable<string>? days,
        IEnumerable<int>? roomIds,
        IEnumerable<int>? tagIds,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _client.GetSessionsAsync(cancellationToken);

        var dayFilter = days?
            .Select(d => DateTime.TryParse(d, out var dt) ? dt.Date : (DateTime?)null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToHashSet();

        var roomFilter = roomIds?.ToHashSet();
        var tagFilter = tagIds?.ToHashSet();

        return sessions
            .Where(s => s.StartsAt.HasValue)
            .Where(s => dayFilter is null || dayFilter.Count == 0 || dayFilter.Contains(s.StartsAt!.Value.Date))
            .Where(s => roomFilter is null || roomFilter.Count == 0 || (s.RoomId.HasValue && roomFilter.Contains(s.RoomId.Value)))
            .Where(s => tagFilter is null || tagFilter.Count == 0 || s.CategoryItems.Any(c => tagFilter.Contains(c)))
            .OrderBy(s => s.StartsAt)
            .ToList();
    }

    private static DateTime EventNow() => TimeZoneInfo.ConvertTime(DateTime.UtcNow, EventTimeZone);

    private static TimeZoneInfo ResolveCopenhagenTimeZone()
    {
        // IANA id works on Linux/Mac and on Windows from .NET 8 onwards.
        // Fall back to the Windows id if the IANA lookup fails on an older runtime.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }
}

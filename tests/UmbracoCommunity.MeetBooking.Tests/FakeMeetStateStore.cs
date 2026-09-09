using UmbracoCommunity.MeetBooking.Models;
using UmbracoCommunity.MeetBooking.Storage;

namespace UmbracoCommunity.MeetBooking.Tests;

/// <summary>In-memory <see cref="IMeetStateStore"/> standing in for the EF table.</summary>
public sealed class FakeMeetStateStore : IMeetStateStore
{
    private readonly Dictionary<Guid, (string Json, string? Error)> _rows = [];

    public int WriteCount { get; private set; }

    /// <summary>Fires on each <see cref="Write"/>, so a test can assert ordering against other collaborators.</summary>
    public Action? OnWrite { get; set; }

    public void Seed(Guid recordId, MeetProvisioningState state) => _rows[recordId] = (state.ToJson(), null);

    public string? ErrorFor(Guid recordId) => _rows.TryGetValue(recordId, out var row) ? row.Error : null;

    public MeetProvisioningState? Read(Guid recordId) =>
        _rows.TryGetValue(recordId, out var row) ? MeetProvisioningState.FromJson(row.Json) : null;

    public void Write(Guid recordId, MeetProvisioningState state, string? error)
    {
        WriteCount++;
        OnWrite?.Invoke();
        _rows[recordId] = (state.ToJson(), error);
    }
}

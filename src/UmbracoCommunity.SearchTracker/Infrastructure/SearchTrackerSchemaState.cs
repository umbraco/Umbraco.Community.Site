namespace UmbracoCommunity.SearchTracker.Infrastructure;

/// <summary>Lets background services wait until migrations have run before touching the table.</summary>
public sealed class SearchTrackerSchemaState
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Ready => _ready.Task;

    public void MarkReady() => _ready.TrySetResult();
}

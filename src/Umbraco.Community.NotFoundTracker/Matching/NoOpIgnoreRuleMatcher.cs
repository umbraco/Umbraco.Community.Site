namespace Umbraco.Community.NotFoundTracker.Matching;

internal sealed class NoOpIgnoreRuleMatcher : INotFoundIgnoreRuleMatcher
{
    public bool IsIgnored(string hostname, string path) => false;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

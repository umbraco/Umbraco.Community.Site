namespace Umbraco.Community.NotFoundTracker.Recording;

/// <summary>
/// One occurrence of a 404. Pushed into <see cref="NotFoundHitChannel"/> by the finder
/// (synchronously, non-blocking) and drained by <c>NotFoundHitWriterService</c>.
/// </summary>
public sealed record NotFoundHitEvent(
    string Hostname,
    string Path,
    string? QueryString,
    string? UserAgent,
    DateTime OccurredUtc);

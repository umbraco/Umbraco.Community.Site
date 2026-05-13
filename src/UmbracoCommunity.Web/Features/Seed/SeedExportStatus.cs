namespace UmbracoCommunity.Web.Features.Seed;

public sealed record SeedExportStatus(
    bool IsRunning,
    DateTimeOffset? LastSuccessAt,
    long? LastSuccessSizeBytes,
    DateTimeOffset? LastFailureAt,
    string? LastError,
    DateTimeOffset? StartedAt);

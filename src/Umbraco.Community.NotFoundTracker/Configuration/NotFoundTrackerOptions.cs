namespace Umbraco.Community.NotFoundTracker.Configuration;

public sealed class NotFoundTrackerOptions
{
    public int ActiveRetentionDays { get; set; } = 90;
    public int ActionedRetentionDays { get; set; } = 7;
    public int QueryStringRetentionDays { get; set; } = 14;
    public TimeSpan RetentionSweepInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan WriterFlushInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int WriterBatchSize { get; set; } = 500;
    public int ChannelCapacity { get; set; } = 10_000;
    public bool SeedAutoPreset { get; set; } = true;

    // Populated by Plan 2 (config-seeded ignore rules).
    public List<AutoPresetRuleConfig> AdditionalAutoPresetRules { get; set; } = new();
}

public sealed class AutoPresetRuleConfig
{
    public string Path { get; set; } = string.Empty;
    public string MatchType { get; set; } = "PathPrefix";   // bound as string; parsed in Plan 2
    public string? Hostname { get; set; }
    public string? Note { get; set; }
}

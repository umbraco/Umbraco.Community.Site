namespace UmbracoCommunity.SearchTracker;

public sealed class SearchTrackerOptions
{
    public const string SectionName = "SearchTracker";

    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 365;
    public int MaxTermLength { get; set; } = 100;
    public int MaxSearchesPerClientPerHour { get; set; } = 60;
    public TimeSpan DuplicateWindow { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan RetentionSweepInterval { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan WriterFlushInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int WriterBatchSize { get; set; } = 500;
    public int ChannelCapacity { get; set; } = 10_000;
}

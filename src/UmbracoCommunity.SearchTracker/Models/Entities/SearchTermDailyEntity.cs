namespace UmbracoCommunity.SearchTracker.Models.Entities;

public class SearchTermDailyEntity
{
    public int Id { get; set; }
    public Guid SiteKey { get; set; }
    public DateOnly Day { get; set; }
    public string Term { get; set; } = string.Empty;
    public int SearchCount { get; set; }
    public int ZeroResultCount { get; set; }
    public int? LastResultCount { get; set; }
}

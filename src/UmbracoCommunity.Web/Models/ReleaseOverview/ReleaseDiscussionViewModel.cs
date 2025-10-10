namespace UmbracoCommunity.Web.Models.ReleaseOverview;

public class ReleaseDiscussionViewModel
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseLabel { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public bool IsReleaseDateTba { get; set; }
    public string Description { get; set; } = string.Empty;
    public int FeatureCount { get; set; }
    public int IssueCount { get; set; }
    public int BreakingChangesCount { get; set; }
    public bool IsReleased => ReleaseDate.HasValue && ReleaseDate.Value <= DateTime.UtcNow;
    public string DiscussionUrl { get; set; } = string.Empty;
    public string FormattedReleaseDate
    {
        get
        {
            if (IsReleaseDateTba)
                return "To be announced";
            if (ReleaseDate.HasValue)
                return ReleaseDate.Value.ToString("dddd, MMMM d yyyy");
            return "To be announced";
        }
    }
}

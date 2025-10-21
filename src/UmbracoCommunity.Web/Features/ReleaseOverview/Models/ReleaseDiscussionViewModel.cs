namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseDiscussionViewModel : ReleaseInfoViewModel
{
    public int FeatureCount { get; set; }
    public int IssueCount { get; set; }
    public int BreakingChangesCount { get; set; }
}

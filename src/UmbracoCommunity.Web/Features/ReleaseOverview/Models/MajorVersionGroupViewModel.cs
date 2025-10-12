namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class MajorVersionGroupViewModel
{
    public int MajorVersion { get; set; }
    public ReleaseDiscussionViewModel? LatestRelease { get; set; }
    public List<ReleaseDiscussionViewModel> OtherReleases { get; set; } = [];
}

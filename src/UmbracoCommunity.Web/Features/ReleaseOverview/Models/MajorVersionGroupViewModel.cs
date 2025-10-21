namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class MajorVersionGroupViewModel
{
    public int MajorVersion { get; set; }
    public ReleaseInfoViewModel? LatestRelease { get; set; }
    public List<ReleaseInfoViewModel> OtherReleases { get; set; } = [];
}

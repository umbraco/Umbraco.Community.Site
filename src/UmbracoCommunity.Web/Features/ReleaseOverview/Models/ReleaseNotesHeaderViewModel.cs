namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseNotesHeaderViewModel
{
    public ReleaseDiscussionViewModel? ReleaseInfo { get; set; }
    public string SelectedRepo { get; set; } = string.Empty;
    public ReleaseGroupViewModel? ReleaseGroup { get; set; }
}

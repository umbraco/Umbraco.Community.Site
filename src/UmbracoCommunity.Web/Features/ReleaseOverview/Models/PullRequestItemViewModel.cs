namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class PullRequestItemViewModel
{
    public ReleasePullRequestViewModel PullRequest { get; set; } = null!;
    public string UmbracoLogoPath { get; set; } = string.Empty;
    public bool LabelCheck { get; set; } = true;
}

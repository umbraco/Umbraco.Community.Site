namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ContributorsSectionViewModel
{
    public ReleaseGroupViewModel ReleaseGroup { get; set; } = null!;
    public string SelectedRepo { get; set; } = string.Empty;
}

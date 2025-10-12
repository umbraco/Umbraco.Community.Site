namespace UmbracoCommunity.Web.Features.ReleaseOverview.Models;

public class ReleaseCategoriesViewModel
{
    public ReleaseGroupViewModel ReleaseGroup { get; set; } = null!;
    public string UmbracoLogoPath { get; set; } = string.Empty;
    public bool LabelCheck { get; set; } = true;
}

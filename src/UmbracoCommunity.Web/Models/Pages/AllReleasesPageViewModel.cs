using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;

namespace UmbracoCommunity.Web.Models.Pages;

public class AllReleasesPageViewModel : PageViewModelBase
{
    public AllReleasesPageViewModel(IPublishedContent currentPage) : base(currentPage)
    {
    }

    // Override to ensure the correct CSS entrypoint is loaded
    public new string ContentTypeAlias => "releasesHome";

    public List<MajorVersionGroupViewModel> VersionGroups { get; set; } = new();
}

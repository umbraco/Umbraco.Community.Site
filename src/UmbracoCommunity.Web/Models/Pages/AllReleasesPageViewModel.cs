using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;

namespace UmbracoCommunity.Web.Models.Pages;

public class AllReleasesPageViewModel : PageViewModelBase
{
    public AllReleasesPageViewModel(IPublishedContent currentPage) : base(currentPage)
    {
    }

    public List<MajorVersionGroupViewModel> VersionGroups { get; set; } = new();
}

using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ReleasesHomePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public IList<BlockGridRow> BlockContent { get; set; } = [];
        public List<ReleaseInfoViewModel> UpcomingReleases { get; set; } = new();
        public ReleaseInfoViewModel? LatestRelease { get; set; }
        public List<ReleaseInfoViewModel> LtsReleases { get; set; } = new();
    }
}

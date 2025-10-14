using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ReleasesHomePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public IList<BlockGridRow> BlockContent { get; set; } = [];
        public List<ReleaseDiscussionViewModel> UpcomingReleases { get; set; } = new();
        public ReleaseDiscussionViewModel? LatestRelease { get; set; }
        public List<ReleaseDiscussionViewModel> LtsReleases { get; set; } = new();
    }
}

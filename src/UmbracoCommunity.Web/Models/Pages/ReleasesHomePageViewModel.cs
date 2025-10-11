using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.ReleaseOverview;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ReleasesHomePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public IList<BlockGridRow> BlockContent { get; set; } = [];
        
        public List<ReleaseGroupViewModel> Releases { get; set; } = new();
        public string? SelectedRepo { get; set; }
        public string? SelectedRelease { get; set; }
        public string? CompareRelease1 { get; set; }
        public string? CompareRelease2 { get; set; }
        public List<string> AvailableReleases { get; set; } = new();
        public string UmbracoLogoPath { get; set; } = "/img/umbraco_logo.png";
        public List<ReleaseDiscussionViewModel> UpcomingReleases { get; set; } = new();
        public ReleaseDiscussionViewModel? LatestRelease { get; set; }
        public List<ReleaseDiscussionViewModel> LtsReleases { get; set; } = new();
        public ReleaseDiscussionViewModel? ReleaseInfo { get; set; }
    }
}

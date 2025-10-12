using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ReleasesHomePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public IList<BlockGridRow> BlockContent { get; set; } = [];
        
        public List<ReleaseGroupViewModel> Releases { get; set; } = new();
        public string? SelectedRepo { get; set; }
        public string? SelectedRelease { get; set; }
        public List<string> AvailableReleases { get; set; } = new();
        public string UmbracoLogoPath { get; set; } = "/img/umbraco_logo.png";
        public List<ReleaseDiscussionViewModel> UpcomingReleases { get; set; } = new();
        public ReleaseDiscussionViewModel? LatestRelease { get; set; }
        public List<ReleaseDiscussionViewModel> LtsReleases { get; set; } = new();
        public ReleaseDiscussionViewModel? ReleaseInfo { get; set; }
        public bool LabelCheck { get; set; }
    }
}

using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ReleasePageViewModel : PageViewModelBase
    { 
        public ReleasePageViewModel(IPublishedContent currentPage) : base(currentPage)
        {
            // Override to ensure the correct CSS entrypoint is loaded
            ContentTypeAlias = "releasesHome";
        }
        public string Organization { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ReleaseLabel { get; set; } = string.Empty;
        public ReleaseDiscussionViewModel? ReleaseInfo { get; set; }
        public ReleaseGroupViewModel? Release { get; set; }
        public string? NuGetPackageId { get; set; }
        public bool LabelCheck { get; set; }
    }
}
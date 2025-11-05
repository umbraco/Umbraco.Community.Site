using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class HomePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public BlockGridModel? Banner { get; set; }
        public IList<BlockGridRow> BlockContent { get; set; } = [];
    }
}

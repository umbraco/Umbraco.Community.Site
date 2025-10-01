using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class HomePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public IList<BlockGridRow> BlockContent { get; set; } = [];
    }
}

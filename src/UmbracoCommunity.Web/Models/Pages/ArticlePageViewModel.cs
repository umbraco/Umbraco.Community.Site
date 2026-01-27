using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ArticlePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
        public string? Teaser { get; set; }

        public IList<BlockGridRow> BlockContent { get; set; } = [];

        public DateTime PublishDate { get; set; }

        public int ReadTime { get; set; }
    }
}

using Umbraco.Cms.Core.Models.Blocks;
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

        public List<string> Tags { get; set; } = [];

        public string? BlogUrl { get; set; }

        public List<IPublishedContent> Categories { get; set; } = [];

        public BlockGridModel? Banner { get; internal set; }

        public ArticleAuthorViewModel? Author { get; set; }
    }
}

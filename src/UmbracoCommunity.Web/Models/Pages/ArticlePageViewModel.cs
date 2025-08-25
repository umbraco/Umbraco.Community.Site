using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ArticlePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
    }
}

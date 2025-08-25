using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class BlogPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
    }
}

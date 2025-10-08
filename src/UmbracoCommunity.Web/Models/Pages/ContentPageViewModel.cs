using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class ContentPageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
    }
}

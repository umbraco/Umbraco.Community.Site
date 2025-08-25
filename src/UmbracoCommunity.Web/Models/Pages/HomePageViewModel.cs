using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages
{
    public class HomePageViewModel(IPublishedContent currentPage) : PageViewModelBase(currentPage)
    {
    }
}

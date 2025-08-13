using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.Pages.Testing
{
    public class TestHomePageViewModel : PageViewModelBase
    {
        public TestHomePageViewModel(IPublishedContent currentPage)
            : base(currentPage)
        {
        }

        public string? MediaItemUrl { get; set; } = null;
    }
}

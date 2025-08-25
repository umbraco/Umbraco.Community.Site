using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.Pages;

namespace UmbracoCommunity.Web.ViewModelBuilders.Pages
{
    internal class HomePageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<HomePageViewModel>
    {
        public HomePageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
        {
            return new HomePageViewModel(currentPage);
        }
    }
}

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Components;

namespace UmbracoCommunity.Web.ViewModelBuilders.Components;

internal class FooterViewModelBuilder : IViewModelBuilder<FooterViewModel>
{
    public FooterViewModelBuilder(IPublishedContentQuery publishedContentQuery, IPublishedUrlProvider publishedUrlProvider, AppCaches appCaches)
    {
    }

    public FooterViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        FooterViewModel viewModel = new();

        var siteSettings = currentPage.GetSiteSettings();
        viewModel.Logo = siteSettings?.FooterLogo ?? siteSettings?.HeaderLogo; // fallback to header logo as default

        var navSettings = currentPage.GetNavigationSettings(siteSettings);
        if (navSettings != null)
        {
            viewModel.LinkColumns = navSettings.FooterLinks;
            if (navSettings.SocialSharingLinks != null && navSettings.SocialSharingLinks.Count > 0)
            {
                viewModel.SocialLinks = [.. navSettings.SocialSharingLinks.OfType<BlockGridItem<LinkWithIcon>>()];
            }
            if (navSettings.BottomLinks != null && navSettings.BottomLinks.Any())
            {
                viewModel.BottomLinks = navSettings?.BottomLinks.ToList();
            }
        }

        return viewModel;
    }
}

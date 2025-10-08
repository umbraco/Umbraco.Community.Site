using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using static UmbracoCommunity.Web.Models.ViewModels.Components.MenuViewModel;

namespace UmbracoCommunity.Web.ViewModelBuilders.Components;

internal class MenuViewModelBuilder : NavigationViewModelBuilderBase, IViewModelBuilder<MenuViewModel>
{
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IImageUrlGenerator _imageUrlGenerator;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IFileService _fileService;

    public MenuViewModelBuilder(
        IPublishedContentQuery publishedContentQuery,
        IPublishedUrlProvider publishedUrlProvider,
        IImageUrlGenerator imageUrlGenerator,
        IPublishedValueFallback publishedValueFallback,
        IFileService fileService,
        AppCaches appCaches)
        : base(publishedContentQuery, publishedUrlProvider, appCaches)
    {
        _publishedUrlProvider = publishedUrlProvider;
        _imageUrlGenerator = imageUrlGenerator;
        _publishedValueFallback = publishedValueFallback;
        _fileService = fileService;
    }

    public MenuViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        return CreateViewModelAndPopulateTopLevelNavigation(currentPage);
    }

    private MenuViewModel CreateViewModelAndPopulateTopLevelNavigation(IPublishedContent currentPage)
    {
        MenuViewModel viewModel = new();

        viewModel.AddTopLevelNavigationItem(new NavigationItem(new Link { Name = "Events", Url = "events" }));

        var devLearnSection = new NavigationSection
        {
            Headline = "Source"
        };
        devLearnSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Documentation", Url = "docs" }));
        devLearnSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Packages", Url = "packages" }));
        devLearnSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Download", Url = "download" }));

        var devConnectSection = new NavigationSection
        {
            Headline = "Help"
        };
        devConnectSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Contribute", Url = "contribute" }));
        devConnectSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Forum", Url = "forum" }));
        devConnectSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Discord", Url = "discord" }));

        var devDropdown = new NavigationDropdown("Developer", 2);
        devDropdown.AddSection(devLearnSection);
        devDropdown.AddSection(devConnectSection);

        viewModel.AddTopLevelNavigationItem(devDropdown);

        var communityInvolvedSection = new NavigationSection
        {
            Headline = "Get involved"
        };
        communityInvolvedSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Community teams", Url = "teams" }));
        communityInvolvedSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Advisory boards", Url = "boards" }));
        communityInvolvedSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Guilds", Url = "guilds" }));

        var communityRecognitionSection = new NavigationSection
        {
            Headline = "Advocate"
        };
        communityRecognitionSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "MVP Program", Url = "mvps" }));
        communityRecognitionSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Contribute as an agency", Url = "agency" }));

        var communityDropdown = new NavigationDropdown("Community", 2);
        communityDropdown.AddSection(communityInvolvedSection);
        communityDropdown.AddSection(communityRecognitionSection);

        viewModel.AddTopLevelNavigationItem(communityDropdown);

        var deiSection = new NavigationSection
        {
            Headline = "About"
        };
        deiSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Diversity, Equity and Inclusion group", Url = "dei" }));
        deiSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Mission", Url = "dei-mission" }));
        deiSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Releases 2025", Url = "releases" }));

        var deiDropdown = new NavigationDropdown("Diversity", 1);
        deiDropdown.AddSection(deiSection);

        viewModel.AddTopLevelNavigationItem(deiDropdown);

        var devrelSection = new NavigationSection
        {
            Headline = "About"
        };
        devrelSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "About us", Url = "about" }));
        devrelSection.AddSectionItem(new NavigationSectionItem(new Link { Name = "Contact", Url = "contact" }));

        var devrelDropdown = new NavigationDropdown("DevRel", 1);
        devrelDropdown.AddSection(devrelSection);

        viewModel.AddTopLevelNavigationItem(devrelDropdown);

        viewModel.AddCtaButton(new Link { Name = "Sign in", Url = "login" });

        return viewModel;
    }
}

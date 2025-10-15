using Umbraco.Cms.Core.Models;
using UmbracoCommunity.Web.Models.ViewModels.Components.Navigation;

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class MenuViewModel : ICloneable
{
    private readonly List<INavigationElement> _topLevelNavigationItems = [];
    private readonly List<NavigationLink> _ctaButtons = [];

    public MenuViewModel() { }

    private MenuViewModel(MenuViewModel viewModel)
    {
        _topLevelNavigationItems = viewModel._topLevelNavigationItems.Select(x => (INavigationElement)x.Clone()).ToList();
        _ctaButtons = viewModel.CallToActionButtons.Select(x => (NavigationLink)x.Clone()).ToList();
        SearchPageUrl = viewModel.SearchPageUrl;
        HeroLayout = viewModel.HeroLayout;
    }

    public bool HasSearchPage => !string.IsNullOrEmpty(SearchPageUrl);

    public bool HasHeroLayout => !string.IsNullOrEmpty(HeroLayout);

    public string? SearchPageUrl { get; set; }

    public string? HeroLayout { get; set; }

    public void AddCtaButton(Link link) => _ctaButtons.Add(new NavigationLink(link));

    public void ClearCtaButtons() => _ctaButtons.Clear();

    public IReadOnlyList<INavigationElement> TopLevelNavigationItems => _topLevelNavigationItems.AsReadOnly();

    public IReadOnlyList<NavigationLink> CallToActionButtons => _ctaButtons;

    public void AddTopLevelNavigationItem(INavigationElement navigationElement) => _topLevelNavigationItems.Add(navigationElement);

    public object Clone() => new MenuViewModel(this);
}

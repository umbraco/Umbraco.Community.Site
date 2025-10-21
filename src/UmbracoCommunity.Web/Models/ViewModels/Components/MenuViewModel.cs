using UmbracoCommunity.Web.Models.ViewModels.Components.Navigation;

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public class MenuViewModel : ICloneable
{
    private readonly List<INavigationElement> _topLevelNavigationItems = [];

    public MenuViewModel() { }

    private MenuViewModel(MenuViewModel viewModel)
    {
        _topLevelNavigationItems = viewModel._topLevelNavigationItems.Select(x => (INavigationElement)x.Clone()).ToList();
        SearchPageUrl = viewModel.SearchPageUrl;
        HeroLayout = viewModel.HeroLayout;
    }

    public bool HasSearchPage => !string.IsNullOrEmpty(SearchPageUrl);

    public bool HasHeroLayout => !string.IsNullOrEmpty(HeroLayout);

    public string? SearchPageUrl { get; set; }

    public string? HeroLayout { get; set; }

    public NavigationLink? CallToActionButton { get; set; }

    public IReadOnlyList<INavigationElement> TopLevelNavigationItems => _topLevelNavigationItems.AsReadOnly();

    public void AddTopLevelNavigationItem(INavigationElement navigationElement) => _topLevelNavigationItems.Add(navigationElement);

    public object Clone() => new MenuViewModel(this);
}
